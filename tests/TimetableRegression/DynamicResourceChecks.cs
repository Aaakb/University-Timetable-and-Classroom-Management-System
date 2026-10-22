using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class DynamicResourceChecks
{
    public static async Task RunAsync(Func<string, Func<Task>, Task> check)
    {
        string? previousConnection = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
        string database = "UniversityTimetable_DynamicResources_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost",
            InitialCatalog = database,
            IntegratedSecurity = true,
            Encrypt = false,
            TrustServerCertificate = true
        };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        Console.WriteLine("ISOLATED DATABASE: " + database);
        try
        {
            await RunChecksAsync(check);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, previousConnection);
        }
    }

    private static async Task RunChecksAsync(Func<string, Func<Task>, Task> check)
    {
        var maintenance = new DatabaseMaintenanceService();
        var rooms = new ClassroomService();
        var schedules = new ScheduleService();
        var periods = new TimeSlotService();
        await maintenance.ApplyPendingFixesAsync();

        await check("New databases retain the existing room and time defaults", async () =>
        {
            var defaults = await rooms.GetAllAsync();
            Ensure(defaults.Count == 11 && defaults.Count(ClassroomRules.IsLab) == 7 &&
                defaults.Count(ClassroomRules.IsSharedLab) == 2, "Unexpected initial rooms");
            Ensure((await periods.GetAllAsync()).Count == 3, "Unexpected initial periods");
        });

        await check("Explicit room types override names while legacy classification still works", () =>
        {
            Ensure(!ClassroomRules.IsLab(new() { RoomType = " lecture ", ClassroomNumber = "Lab Annex" }), "Lecture type overridden");
            Ensure(ClassroomRules.IsLab(new() { RoomType = " lab ", ClassroomNumber = "Room 1" }), "Lab type ignored");
            Ensure(ClassroomRules.IsLab(new() { RoomType = null, ClassroomNumber = "Legacy Lab" }), "Legacy lab lost");
            Ensure(!ClassroomRules.IsLab(new() { RoomType = null, ClassroomNumber = "Legacy Hall" }), "Legacy hall lost");
            var shared = new Classroom { RoomType = " shared lab ", ClassroomNumber = "Room 2" };
            Ensure(ClassroomRules.IsSharedLab(shared) && ClassroomRules.IsLab(shared), "Shared type lost");
            return Task.CompletedTask;
        });

        var room = await rooms.AddAsync(new() { ClassroomNumber = "Lab Annex", RoomType = "Lab", Capacity = 30 });
        await check("Changing an unused lab to Lecture respects the selected type and saved name", async () =>
        {
            room.RoomType = " lecture ";
            await rooms.UpdateAsync(room);
            var saved = (await rooms.GetByIdAsync(room.ClassroomID))!;
            Ensure(saved.RoomType == "Lecture" && saved.ClassroomNumber == "Lab Annex" && !ClassroomRules.IsLab(saved), "Type change was ignored");
        });

        foreach (var original in (await rooms.GetAllAsync()).Where(item => item.ClassroomID != room.ClassroomID))
            await rooms.DeleteAsync(original.ClassroomID);
        await check("Repeated startup preserves a single configured room without replenishment", async () =>
        {
            string before = await SnapshotAsync();
            await maintenance.ApplyPendingFixesAsync();
            await maintenance.ApplyPendingFixesAsync();
            Ensure(await SnapshotAsync() == before, "Startup changed configured resources");
        });

        var year = await new StudyYearService().AddAsync(new() { YearName = "First Year" });
        await new SectionService().AddAsync(new() { StudyYearID = year.StudyYearID, SectionName = "A", StudentCount = 30 });
        var teacher = await new FacultyMemberService().AddAsync(new() { FullName = "Dynamic Resource Teacher" });
        var subject = await new SubjectService().AddAsync(new()
        {
            StudyYearID = year.StudyYearID, SubjectName = "Dynamic Theory", SemesterNumber = 1,
            TheoreticalHours = 1, CreditUnits = 1, RequirementType = "Core"
        });
        await new FacultyMemberSubjectService().AddAsync(new() { SubjectID = subject.SubjectID, FacultyMemberID = teacher.FacultyMemberID });

        await check("Generation uses the only eligible room without creating rooms or periods", async () =>
        {
            var result = await schedules.GenerateAsync();
            var saved = await schedules.GetAllAsync();
            Ensure(result.CreatedCount == 1 && result.RequiredCount == 1 && result.ClassroomCount == 1, "Incorrect dynamic counts");
            Ensure(result.AddedClassroomCount == 0 && result.AddedTimeSlotCount == 0, "Generation created resources");
            Ensure(saved.Single().ClassroomID == room.ClassroomID && (await rooms.GetAllAsync()).Count == 1, "Wrong room selected");
        });

        await check("Reducing capacity below a saved lesson's requirement preserves the data", async () =>
        {
            string before = await SnapshotAsync();
            var edited = (await rooms.GetByIdAsync(room.ClassroomID))!;
            edited.Capacity = 29;
            await RejectAsync(() => rooms.UpdateAsync(edited));
            Ensure(await SnapshotAsync() == before, "Rejected capacity edit changed data");
        });

        await check("An incompatible type change preserves the saved timetable", async () =>
        {
            string before = await SnapshotAsync();
            var edited = (await rooms.GetByIdAsync(room.ClassroomID))!;
            edited.RoomType = "Lab";
            await RejectAsync(() => rooms.UpdateAsync(edited));
            Ensure(await SnapshotAsync() == before, "Rejected type edit changed data");
        });

        await check("Deleting a booked classroom is blocked by the existing foreign key", async () =>
        {
            string before = await SnapshotAsync();
            try
            {
                await rooms.DeleteAsync(room.ClassroomID);
                throw new Exception("Booked room was deleted");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 }) { }
            Ensure(await SnapshotAsync() == before, "Rejected deletion changed data");
        });

        foreach (var lesson in await schedules.GetAllAsync()) await schedules.DeleteAsync(lesson.ScheduleID);
        foreach (var savedRoom in await rooms.GetAllAsync()) await rooms.DeleteAsync(savedRoom.ClassroomID);
        await check("Deleting every room stays effective after restart and failed generation", async () =>
        {
            string before = await SnapshotAsync();
            await maintenance.ApplyPendingFixesAsync();
            await RejectAsync(() => schedules.GenerateAsync(), "Add classrooms");
            Ensure(await SnapshotAsync() == before && (await rooms.GetAllAsync()).Count == 0, "Empty room configuration was repopulated");
        });

        for (int i = 0; i < 20; i++)
            await rooms.AddAsync(new() { ClassroomNumber = $"Small Hall {i:00}", RoomType = "Lecture", Capacity = 5 });
        var extra = await rooms.AddAsync(new() { ClassroomNumber = "Additional Hall", RoomType = "Lecture", Capacity = 30 });
        await check("A newly added eligible room beyond the original counts participates in generation", async () =>
        {
            var result = await schedules.GenerateAsync();
            Ensure(result.ClassroomCount == 21 && result.CreatedCount == result.RequiredCount, "Fixed room count still applied");
            Ensure((await schedules.GetAllAsync()).All(lesson => lesson.ClassroomID == extra.ClassroomID), "Capacity or new room ignored");
        });

        await check("New subjects and teaching assignments are recognized by the next generation", async () =>
        {
            var secondTeacher = await new FacultyMemberService().AddAsync(new() { FullName = "Additional Teacher" });
            var secondSubject = await new SubjectService().AddAsync(new()
            {
                StudyYearID = year.StudyYearID, SubjectName = "Additional Theory", SemesterNumber = 1,
                TheoreticalHours = 1, CreditUnits = 1, RequirementType = "Core"
            });
            await new FacultyMemberSubjectService().AddAsync(new() { SubjectID = secondSubject.SubjectID, FacultyMemberID = secondTeacher.FacultyMemberID });
            var result = await schedules.GenerateAsync();
            Ensure(result.CreatedCount == 2 && result.RequiredCount == 2, "New subject ignored");
            var saved = await schedules.GetAllAsync();
            Ensure(saved.Single(lesson => lesson.SubjectID == secondSubject.SubjectID).FacultyMemberID == secondTeacher.FacultyMemberID, "New assignment ignored");
            Ensure(saved.Select(lesson => (lesson.DayOfWeek, lesson.TimeSlotID, lesson.ClassroomID)).Distinct().Count() == 2, "Room conflict");
        });

        foreach (var lesson in await schedules.GetAllAsync()) await schedules.DeleteAsync(lesson.ScheduleID);
        foreach (var period in await periods.GetAllAsync()) await periods.DeleteAsync(period.TimeSlotID);
        await check("Deleting every period stays effective after restart and failed generation", async () =>
        {
            string before = await SnapshotAsync();
            await maintenance.ApplyPendingFixesAsync();
            await RejectAsync(() => schedules.GenerateAsync(), "Add lecture periods");
            Ensure(await SnapshotAsync() == before && (await periods.GetAllAsync()).Count == 0, "Empty calendar was repopulated");
        });

        await check("Adding a custom period restores generation using only that period", async () =>
        {
            var custom = await periods.AddAsync(new() { StartTime = new(14, 0, 0), EndTime = new(15, 30, 0) });
            var result = await schedules.GenerateAsync();
            Ensure(result.CreatedCount == 2 && result.TimeSlotCount == 1 && result.AddedTimeSlotCount == 0, "Custom calendar ignored");
            Ensure((await schedules.GetAllAsync()).All(lesson => lesson.TimeSlotID == custom.TimeSlotID), "Unexpected time slot used");
        });

        await check("Startup does not rewrite legacy room types or existing schedules", async () =>
        {
            await using (var context = new AppDbContext())
            {
                context.Classrooms.Add(new() { ClassroomID = 500, ClassroomNumber = "Legacy Lab", RoomType = null, Capacity = 20 });
                await context.SaveChangesAsync();
            }
            string before = await SnapshotAsync();
            await maintenance.ApplyPendingFixesAsync();
            Ensure(await SnapshotAsync() == before, "Startup rewrote saved data");
        });
    }

    private static async Task<string> SnapshotAsync()
    {
        await using var context = new AppDbContext();
        return JsonSerializer.Serialize(new
        {
            Rooms = await context.Classrooms.AsNoTracking().OrderBy(room => room.ClassroomID)
                .Select(room => new { room.ClassroomID, room.ClassroomNumber, room.RoomType, room.Capacity }).ToListAsync(),
            Periods = await context.TimeSlots.AsNoTracking().OrderBy(period => period.TimeSlotID)
                .Select(period => new { period.TimeSlotID, period.StartTime, period.EndTime, period.IsBreak }).ToListAsync(),
            Lessons = await context.Schedules.AsNoTracking().OrderBy(lesson => lesson.ScheduleID)
                .Select(lesson => new { lesson.ScheduleID, lesson.SubjectID, lesson.FacultyMemberID, lesson.ClassroomID,
                    lesson.TimeSlotID, lesson.SectionID, lesson.StudyYearID, lesson.BranchID, lesson.SemesterNumber,
                    lesson.DayOfWeek, lesson.LectureType, lesson.GroupName }).ToListAsync()
        });
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static async Task RejectAsync(Func<Task> operation, string? messagePart = null)
    {
        try { await operation(); }
        catch (ArgumentException ex) when (messagePart is null || ex.Message.Contains(messagePart)) { return; }
        throw new Exception("Expected a meaningful rejection");
    }
}
