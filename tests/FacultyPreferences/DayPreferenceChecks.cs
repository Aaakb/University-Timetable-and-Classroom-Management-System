using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Data_Access_Layer;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static partial class Program
{
    static FacultyScheduleRequest DayProfile(int faculty = 1, string preferred = "Tuesday", bool onlyPreferredDay = false) =>
        new(faculty, 1, FacultyAvailabilityMode.Strict, [], TimeLimitSeconds: 5)
        {
            UseDayPreferences = true,
            DayPreferences = days.Select(d => new FacultyDaySetting(d, d == preferred ? FacultyPreferenceLevel.StronglyPreferred
                : onlyPreferredDay ? FacultyPreferenceLevel.Prohibited : FacultyPreferenceLevel.Preferred)).ToArray()
        };

    static async Task DayPreferenceChecksAsync()
    {
        await Check("Day preferences update an existing timetable with only one configured faculty", async () =>
        {
            var before = await Rows();
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(), 0);
            var after = await Rows();
            Assert(result.ChangedLectures == 2 && after.Where(s => s.FacultyMemberID == 1).All(s => s.DayOfWeek == "Tuesday"));
            Assert(after.Count == before.Count && after.Select(s => s.ScheduleID).SequenceEqual(before.Select(s => s.ScheduleID)));
            foreach (var row in after)
            {
                var expected = Copy(before.Single(s => s.ScheduleID == row.ScheduleID));
                expected.DayOfWeek = row.DayOfWeek; expected.TimeSlotID = row.TimeSlotID; expected.ClassroomID = row.ClassroomID;
                Assert(Json(expected) == Json(row), "Lecture identity changed");
            }
            Assert(await preferences.GetAsync(2, 1) is null, "A faculty without preferences acquired a synthetic profile");
            Assert((await editor.GetConfiguredFacultyAsync(1)).SetEquals(new[] { 1 }));
        });
        await Check("Faculty without preferences can move when required to make a valid timetable", async () =>
        {
            await using (var db = factory())
            {
                var rows = await db.Schedules.Where(s => s.FacultyMemberID == 2).OrderBy(s => s.ScheduleID).ToListAsync();
                rows[0].DayOfWeek = rows[1].DayOfWeek = "Tuesday"; rows[0].TimeSlotID = 1; rows[1].TimeSlotID = 2;
                await db.SaveChangesAsync();
            }
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(onlyPreferredDay: true), 0);
            Assert(result.Changes.Any(c => c.IsOtherFaculty), "The optimizer did not release a required slot");
            Assert((await Rows()).Where(s => s.FacultyMemberID == 1).All(s => s.DayOfWeek == "Tuesday"));
            Assert(await preferences.GetAsync(2, 1) is null);
        });
        await Check("Allowed days are alternatives and do not force attendance on every selected day", async () =>
        {
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(), 0);
            Assert(result.Profile.Settings.SelectedDays.Count == 5);
            Assert((await Rows()).Where(s => s.FacultyMemberID == 1).Select(s => s.DayOfWeek).Distinct().Count() == 1);
        });
        await Check("An infeasible prohibited locked lecture rolls back both preference and timetable changes", async () =>
        {
            await using (var db = factory()) await db.Schedules.Where(s => s.FacultyMemberID == 1).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsLocked, true));
            var before = Json(await Rows());
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(onlyPreferredDay: true), 0));
            Assert(Json(await Rows()) == before && await preferences.GetAsync(1, 1) is null);
        });
        await Check("A locked lecture outside a soft preferred day stays fixed", async () =>
        {
            await using (var db = factory()) await db.Schedules.Where(s => s.FacultyMemberID == 1).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsLocked, true));
            string before = Json(await Rows());
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(), 0);
            Assert(result.ChangedLectures == 0 && Json(await Rows()) == before);
        });
        await Check("Failed replacement of an existing day profile preserves its revision and timetable", async () =>
        {
            var first = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(onlyPreferredDay: true), 0);
            await using (var db = factory()) await db.Schedules.Where(s => s.FacultyMemberID == 1).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsLocked, true));
            string before = Json(await Rows()), saved = Json(await preferences.GetAsync(1, 1) ?? throw new Exception());
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(preferred: "Thursday", onlyPreferredDay: true), first.Profile.Revision));
            Assert(Json(await Rows()) == before && Json(await preferences.GetAsync(1, 1) ?? throw new Exception()) == saved);
        });
        await Check("A database write failure rolls back saved preferences and deleted lecture rows together", async () =>
        {
            string before = Json(await Rows());
            await using var db = factory();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(db.Database.GetConnectionString()).AddInterceptors(new FailDayScheduleWrite()).Options;
            var failing = new FacultyScheduleEditorService(() => new AppDbContext(options));
            await Reject(() => failing.SaveDayPreferencesAndOptimizeAsync(DayProfile(), 0));
            Assert(Json(await Rows()) == before && await preferences.GetAsync(1, 1) is null);
        });
        await Check("Replacing legacy periods with days removes all time restrictions and stored period cells", async () =>
        {
            var old = await preferences.SaveAsync(Profile(), 0);
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(onlyPreferredDay: true) with
                { EarliestStartMinute = 1000, LatestEndMinute = 1100, PreferredPeriodIds = [3], MaxTeachingDays = 2, MaxDailyMinutes = 1 }, old.Revision);
            var saved = (await preferences.GetAsync(1, 1))!.Settings;
            Assert(saved.UseDayPreferences && !saved.UsePreferenceMatrix && saved.PeriodPreferences.Count == 0 && saved.PreferredPeriodIds!.Count == 0);
            Assert(saved.EarliestStartMinute is null && saved.LatestEndMinute is null && saved.MaxDailyMinutes is null);
            await using var db = factory(); Assert(!await db.FacultyPeriodPreferences.AnyAsync());
            Assert((await Rows()).Where(s => s.FacultyMemberID == 1).Any(s => s.TimeSlotID != 3));
        });
        await Check("Saving before timetable creation stores only days and later generation respects them", async () =>
        {
            await using (var db = factory()) await db.Schedules.ExecuteDeleteAsync();
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile() with
            {
                DayPreferences = days.Select(d => new FacultyDaySetting(d, d == "Thursday" ? FacultyPreferenceLevel.Prohibited
                    : d == "Tuesday" ? FacultyPreferenceLevel.StronglyPreferred : FacultyPreferenceLevel.Preferred)).ToArray()
            }, 0);
            Assert(result.Status == "NoTimetable" && (await Rows()).Count == 0);
            await new ScheduleService().GenerateAsync();
            var own = (await Rows()).Where(s => s.FacultyMemberID == 1).ToList();
            Assert(own.Count == 4 && own.All(s => s.DayOfWeek != "Thursday") && own.Any(s => s.DayOfWeek == "Tuesday"));
        });
        await Check("Later faculty entries retain earlier faculty days and optional profiles can be removed", async () =>
        {
            await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(onlyPreferredDay: true), 0);
            var second = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(2, "Thursday", true), 0);
            var rows = await Rows();
            Assert(rows.Where(s => s.FacultyMemberID == 1).All(s => s.DayOfWeek == "Tuesday"));
            Assert(rows.Where(s => s.FacultyMemberID == 2).All(s => s.DayOfWeek == "Thursday"));
            await preferences.DeleteAsync(2, 1, second.Profile.Revision);
            Assert(await preferences.GetAsync(2, 1) is null && (await editor.GetConfiguredFacultyAsync(1)).SetEquals(new[] { 1 }));
        });
        await Check("Stale and canceled day saves leave both the stored profile and timetable unchanged", async () =>
        {
            var first = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(), 0);
            string before = Json(await Rows()), profileBefore = Json(await preferences.GetAsync(1, 1) ?? throw new Exception());
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(preferred: "Thursday"), 0));
            using var cts = new CancellationTokenSource(); cts.Cancel();
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(preferred: "Thursday"), first.Profile.Revision, cts.Token));
            Assert(Json(await Rows()) == before && Json(await preferences.GetAsync(1, 1) ?? throw new Exception()) == profileBefore);
        });
        await Check("Incomplete duplicate or wholly prohibited day profiles are rejected", async () =>
        {
            var p = DayProfile();
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(p with { DayPreferences = p.DayPreferences.Take(4).ToArray() }, 0));
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(p with { DayPreferences = Enumerable.Repeat(p.DayPreferences[0], 5).ToArray() }, 0));
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(p with { DayPreferences = days.Select(d => new FacultyDaySetting(d, FacultyPreferenceLevel.Prohibited)).ToArray() }, 0));
            Assert(await preferences.GetAsync(1, 1) is null);
        });
        await Check("Day optimization preserves subgroup practical exchange and lab compatibility", async () =>
        {
            await using (var db = factory())
            {
                await db.Schedules.ExecuteDeleteAsync();
                db.Classrooms.AddRange(new Classroom { ClassroomID = 3, ClassroomNumber = "Lab 1", RoomType = "Lab", Capacity = 60 },
                    new Classroom { ClassroomID = 4, ClassroomNumber = "Lab 2", RoomType = "Lab", Capacity = 60 });
                await db.Subjects.Where(s => s.SubjectID == 1 || s.SubjectID == 3).ExecuteUpdateAsync(x => x.SetProperty(s => s.PracticalHours, 2));
                await db.SaveChangesAsync();
                var practicals = new List<Schedule>();
                foreach (int subject in new[] { 1, 3 }) foreach (string group in new[] { "A1", "A2" })
                    practicals.Add(new() { SubjectID = subject, FacultyMemberID = subject == 1 ? 1 : 2, SectionID = 1, StudyYearID = 1,
                        SemesterNumber = 1, LectureType = "Practical", GroupName = group, DayOfWeek = "Sunday",
                        TimeSlotID = (subject == 1) == (group == "A1") ? 1 : 2, ClassroomID = subject == 1 ? 3 : 4 });
                db.Schedules.AddRange(practicals); await db.SaveChangesAsync();
            }
            try
            {
                var before = await Rows();
                var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(onlyPreferredDay: true), 0);
                var after = await Rows();
                Assert(result.ChangedLectures == 4 && after.All(s => s.DayOfWeek == "Tuesday" && s.LectureType == "Practical" && s.ClassroomID is 3 or 4));
                foreach (string group in new[] { "A1", "A2" }) Assert(after.Where(s => s.GroupName == group).Select(s => s.TimeSlotID).Distinct().Count() == 2);
                var first = after.Where(s => s.SubjectID == 1).OrderBy(s => s.GroupName).ToList();
                var second = after.Where(s => s.SubjectID == 3).OrderBy(s => s.GroupName).ToList();
                Assert(first[0].TimeSlotID == second[1].TimeSlotID && first[1].TimeSlotID == second[0].TimeSlotID);
                Assert(Math.Abs(first[0].TimeSlotID - first[1].TimeSlotID) == 1 && after.Select(s => s.ScheduleID).SequenceEqual(before.Select(s => s.ScheduleID)));
            }
            finally
            {
                await using var db = factory(); await db.Schedules.ExecuteDeleteAsync();
                await db.Subjects.Where(s => s.SubjectID == 1 || s.SubjectID == 3).ExecuteUpdateAsync(x => x.SetProperty(s => s.PracticalHours, 0));
                await db.Classrooms.Where(c => c.ClassroomID == 3 || c.ClassroomID == 4).ExecuteDeleteAsync();
            }
        });
    }
    private sealed class FailDayScheduleWrite : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Schedule>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Injected schedule write failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
