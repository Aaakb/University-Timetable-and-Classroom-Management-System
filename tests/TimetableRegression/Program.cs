using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

static class Regression
{
    static readonly string Output = Path.GetFullPath(".tmp/FourFixes/results");
    static readonly List<object> Results = [];
    static int failures;
    static readonly SubjectService Subjects = new();
    static readonly ScheduleService Schedules = new();
    static readonly FacultyMemberSubjectService Teaching = new();
    static readonly string TestServer = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost";
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(Output);
        if (args.Contains("resources")) { DynamicResourceChecks.RunAsync(Check).GetAwaiter().GetResult(); WriteUiResults("dynamic-resource-checks.json"); return; }
        if (args.Contains("timing")) { Timing().GetAwaiter().GetResult(); WriteUiResults("timing-checks.json"); return; }
        if (args.Contains("navigation")) { UiNavigation(); return; }
        if (args.Contains("ui")) { Ui(args.Contains("auto-close")); return; }
        Run().GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(Output,"checks.json"), JsonSerializer.Serialize(Results,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SUMMARY: {Results.Count} checks, {failures} failures");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    static void Assert(bool pass, string message="Assertion failed") { if(!pass) throw new Exception(message); }
    static async Task Check(string name, Func<Task> test)
    {
        try { await test(); Results.Add(new {name,passed=true}); Console.WriteLine("PASS " + name); }
        catch(Exception ex) { failures++; Results.Add(new {name,passed=false,error=ex.ToString()}); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    static async Task Reject(Func<Task> action)
    { try { await action(); } catch(ArgumentException) { return; } throw new Exception("Expected a friendly rejection"); }
    static Subject Subject(string name,int semester=1) => new() {SubjectName=name,StudyYearID=1,SemesterNumber=semester,TheoreticalHours=1,PracticalHours=1,CreditUnits=2,RequirementType="Core"};
    // Create a legacy inconsistent fixture directly; normal services now protect used assignments.
    static async Task RemoveAssignmentFixture(int faculty,int subject)
    {
        await using var context=new AppDbContext();
        await context.FacultyMemberSubjects.Where(a=>a.FacultyMemberID==faculty && a.SubjectID==subject).ExecuteDeleteAsync();
    }
    static async Task<string> Snapshot()
    {
        await using var c = new AppDbContext();
        return JsonSerializer.Serialize(await c.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).Select(s=>new{s.ScheduleID,s.SubjectID,s.SemesterNumber,s.StudyYearID,s.BranchID,s.SectionID,s.ClassroomID,s.FacultyMemberID,s.TimeSlotID,s.DayOfWeek,s.LectureType,s.GroupName}).ToListAsync());
    }
    static async Task Run()
    {
        string db="UniversityTimetable_FourFixes_"+Guid.NewGuid().ToString("N")[..8];
        var connection = new SqlConnectionStringBuilder { DataSource=TestServer, InitialCatalog="master", IntegratedSecurity=true, Encrypt=false, TrustServerCertificate=true };
        using(var master=new SqlConnection(connection.ConnectionString))
        { await master.OpenAsync(); using var cmd=new SqlCommand("SELECT DB_ID(@name)",master); cmd.Parameters.AddWithValue("@name",db); Assert(await cmd.ExecuteScalarAsync() is DBNull); }
        connection.InitialCatalog=db;
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
        File.WriteAllText(Path.Combine(Output,"database.txt"),db);
        Console.WriteLine("ISOLATED DATABASE: "+db);
        await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
        await Check("Startup provisions four halls and seven labs including three shared labs",async()=>{
            var rooms=await new ClassroomService().GetAllAsync();
            Assert(rooms.Count==11 && rooms.Count(ClassroomRules.IsLab)==7 && rooms.Count(ClassroomRules.IsSharedLab)==3);
            Assert(rooms.Where(ClassroomRules.IsSharedLab).Select(r=>r.ClassroomNumber).Order()
                .SequenceEqual(new[]{"Lab 02","Lab 03","Lab 05"}));
            Assert(rooms.Where(r=>!ClassroomRules.IsLab(r)).Select(r=>r.ClassroomNumber).SequenceEqual(Enumerable.Range(1,4).Select(i=>$"Lecture Hall {i:00}")));
            var shared=rooms.First(ClassroomRules.IsSharedLab);shared.ClassroomNumber="Shared teaching room";
            await new ClassroomService().UpdateAsync(shared);
            await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
            await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
            rooms=await new ClassroomService().GetAllAsync();
            Assert(rooms.Count==11 && rooms.Count(ClassroomRules.IsSharedLab)==3);
            Assert(ClassroomRules.IsSharedLab(rooms.Single(r=>r.ClassroomID==shared.ClassroomID)));
        });
        for(int y=1;y<=4;y++) await new StudyYearService().AddAsync(new(){YearName=new[]{"First Year","Second Year","Third Year","Fourth Year"}[y-1]});
        var a=await new SectionService().AddAsync(new(){SectionName="A",StudyYearID=1,StudentCount=30});
        var b=await new SectionService().AddAsync(new(){SectionName="B",StudyYearID=1,StudentCount=30});
        var teacher=await new FacultyMemberService().AddAsync(new(){FullName="Regression Teacher"});
        var sub=await Subjects.AddAsync(Subject("Programming"));
        await Teaching.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher.FacultyMemberID});
        await Check("Shared lab accepts both lecture types and rejects simultaneous bookings",async()=>{
            var room=(await new ClassroomService().GetAllAsync()).First(ClassroomRules.IsSharedLab);
            var theory=await Schedules.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher.FacultyMemberID,ClassroomID=room.ClassroomID,SectionID=a.SectionID,TimeSlotID=1,DayOfWeek="Sunday",LectureType="Theory"});
            var secondTeacher=await new FacultyMemberService().AddAsync(new(){FullName="Shared lab teacher"});
            await Teaching.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=secondTeacher.FacultyMemberID});
            var practical=new Schedule{SubjectID=sub.SubjectID,FacultyMemberID=secondTeacher.FacultyMemberID,ClassroomID=room.ClassroomID,SectionID=b.SectionID,TimeSlotID=1,DayOfWeek="Sunday",LectureType="Practical",GroupName="B1"};
            await Reject(()=>Schedules.AddAsync(practical));
            practical.TimeSlotID=2;practical=await Schedules.AddAsync(practical);
            var regular=(await new ClassroomService().GetAllAsync()).First(r=>ClassroomRules.IsLab(r)&&!ClassroomRules.IsSharedLab(r));
            await Reject(()=>Schedules.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher.FacultyMemberID,ClassroomID=regular.ClassroomID,SectionID=a.SectionID,TimeSlotID=3,DayOfWeek="Sunday",LectureType="Theory"}));
            await Schedules.DeleteAsync(theory.ScheduleID);await Schedules.DeleteAsync(practical.ScheduleID);
            await Teaching.DeleteAsync(secondTeacher.FacultyMemberID,sub.SubjectID);await new FacultyMemberService().DeleteAsync(secondTeacher.FacultyMemberID);
        });
        await Check("Normal generation produces a complete timetable",async()=>{var r=await Schedules.GenerateAsync();Assert(r.CreatedCount==r.RequiredCount && r.CreatedCount>0 && r.SkippedCount==0);});
        await Check("Regeneration does not duplicate lessons",async()=>{int count=(await Schedules.GetAllAsync()).Count; await Schedules.GenerateAsync();Assert((await Schedules.GetAllAsync()).Count==count);});
        await Check("Insufficient theory capacity preserves the complete saved timetable",async()=>{
            string before=await Snapshot();
            await using var context=new AppDbContext();
            var rooms=await context.Classrooms.Where(r=>r.RoomType=="Lecture" || r.RoomType==ClassroomRules.SharedLabType).ToListAsync();
            var capacities=rooms.ToDictionary(r=>r.ClassroomID,r=>r.Capacity);
            try {
                foreach(var room in rooms) room.Capacity=1;
                await context.SaveChangesAsync();
                await Reject(()=>Schedules.GenerateAsync());Assert(await Snapshot()==before);
            } finally {
                foreach(var room in rooms) room.Capacity=capacities[room.ClassroomID];
                await context.SaveChangesAsync();
            }
        });
        await Check("No assignments preserves every existing schedule field",async()=>{
            string before=await Snapshot(); await RemoveAssignmentFixture(teacher.FacultyMemberID,sub.SubjectID);
            try {await Reject(()=>Schedules.GenerateAsync());Assert(await Snapshot()==before);}
            finally {await Teaching.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher.FacultyMemberID});}
        });
        await Check("No matching sections preserves previous timetable",async()=>{
            string before=await Snapshot();var missing=Subject("No sections");missing.StudyYearID=2;
            missing=await Subjects.AddAsync(missing);
            await Teaching.AddAsync(new(){SubjectID=missing.SubjectID,FacultyMemberID=teacher.FacultyMemberID});
            await RemoveAssignmentFixture(teacher.FacultyMemberID,sub.SubjectID);
            try {await Reject(()=>Schedules.GenerateAsync());Assert(await Snapshot()==before);}
            finally {
                await Teaching.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher.FacultyMemberID});
                await Teaching.DeleteAsync(teacher.FacultyMemberID,missing.SubjectID);await Subjects.DeleteAsync(missing.SubjectID);
            }
        });
        await Check("Semester move updates all linked lessons and preserves their IDs",async()=>{
            var before=await Schedules.GetAllAsync();sub.SemesterNumber=2;await Subjects.UpdateAsync(sub);
            var after=await Schedules.GetAllAsync();Assert(after.All(s=>s.SemesterNumber==2));Assert(before.Select(s=>s.ScheduleID).Order().SequenceEqual(after.Select(s=>s.ScheduleID).Order()));
            await new DatabaseMaintenanceService().ApplyPendingFixesAsync();Assert((await Schedules.GetAllAsync()).All(s=>s.SemesterNumber==2));
            sub.SemesterNumber=1;await Subjects.UpdateAsync(sub);
        });
        await Check("Subject name edit preserves timetable",async()=>{string before=await Snapshot();sub.SubjectName="Programming Updated";await Subjects.UpdateAsync(sub);Assert(await Snapshot()==before);});
        await Check("Scheduled subject year change is safely rejected",async()=>{sub.StudyYearID=2;try{await Reject(()=>Subjects.UpdateAsync(sub));Assert((await Subjects.GetByIdAsync(sub.SubjectID))!.StudyYearID==1);}finally{sub.StudyYearID=1;}});
        await Check("Invalid semester is rejected",async()=>{await Reject(()=>Subjects.AddAsync(Subject("Invalid",3)));});
        var occupied=(await Schedules.GetAllAsync()).First(s=>s.LectureType=="Theory");
        var other=await Subjects.AddAsync(Subject("Mathematics",2));
        await Teaching.AddAsync(new(){SubjectID=other.SubjectID,FacultyMemberID=teacher.FacultyMemberID});
        var collision=await Schedules.AddAsync(new(){SubjectID=other.SubjectID,FacultyMemberID=teacher.FacultyMemberID,ClassroomID=occupied.ClassroomID,SectionID=occupied.SectionID,TimeSlotID=occupied.TimeSlotID,DayOfWeek=occupied.DayOfWeek,LectureType="Theory"});
        await Check("Conflicting semester move rolls back subject and all lessons",async()=>{
            string before=await Snapshot();sub.SemesterNumber=2;
            try{await Reject(()=>Subjects.UpdateAsync(sub));Assert(await Snapshot()==before);Assert((await Subjects.GetByIdAsync(sub.SubjectID))!.SemesterNumber==1);await new DatabaseMaintenanceService().ApplyPendingFixesAsync();Assert(await Snapshot()==before);}
            finally{sub.SemesterNumber=1;}
        });
        await Schedules.DeleteAsync(collision.ScheduleID);
        await Check("Semester move permits parallel practical groups with separate staff and rooms",async()=>{
            var parallel=await Subjects.AddAsync(Subject("Parallel practical"));
            var faculty2=await new FacultyMemberService().AddAsync(new(){FullName="Second regression teacher"});
            await Teaching.AddAsync(new(){SubjectID=parallel.SubjectID,FacultyMemberID=teacher.FacultyMemberID});
            await Teaching.AddAsync(new(){SubjectID=parallel.SubjectID,FacultyMemberID=faculty2.FacultyMemberID});
            var labs=(await new ClassroomService().GetAllAsync()).Where(r=>r.RoomType=="Lab").Take(2).ToList();
            foreach(var pair in new[]{(Group:"A1",Teacher:teacher.FacultyMemberID,Room:labs[0].ClassroomID),(Group:"A2",Teacher:faculty2.FacultyMemberID,Room:labs[1].ClassroomID)})
                await Schedules.AddAsync(new(){SubjectID=parallel.SubjectID,FacultyMemberID=pair.Teacher,ClassroomID=pair.Room,SectionID=a.SectionID,TimeSlotID=3,DayOfWeek="Monday",LectureType="Practical",GroupName=pair.Group});
            parallel.SemesterNumber=2;await Subjects.UpdateAsync(parallel);
            var moved=(await Schedules.GetAllAsync()).Where(s=>s.SubjectID==parallel.SubjectID).ToList();
            Assert(moved.Count==2 && moved.All(s=>s.SemesterNumber==2));
            foreach(var lesson in moved) await Schedules.DeleteAsync(lesson.ScheduleID);
            await Teaching.DeleteAsync(teacher.FacultyMemberID,parallel.SubjectID);await Teaching.DeleteAsync(faculty2.FacultyMemberID,parallel.SubjectID);
            await Subjects.DeleteAsync(parallel.SubjectID);await new FacultyMemberService().DeleteAsync(faculty2.FacultyMemberID);
        });
        await Check("Unscheduled subject can change semester",async()=>{other.SemesterNumber=1;await Subjects.UpdateAsync(other);Assert((await Subjects.GetByIdAsync(other.SubjectID))!.SemesterNumber==1);});
        await Check("PDF English Arabic mixed empty and multi-page exports",async()=>{
            var pdf=new SchedulePdfExportService();
            var rows=(await Schedules.GetAllAsync()).Select(s=>new SchedulePdfRow(s.SemesterNumber,s.StudyYear!.YearName,s.Branch?.BranchName??"General",s.Section!.SectionName,s.GroupName??"",s.LectureType,s.Subject.SubjectName,s.FacultyMember.FullName,s.Classroom.ClassroomNumber,s.DayOfWeek,DateTime.Today.Add(s.TimeSlot.StartTime).ToString("hh:mm tt",System.Globalization.CultureInfo.InvariantCulture),DateTime.Today.Add(s.TimeSlot.EndTime).ToString("hh:mm tt",System.Globalization.CultureInfo.InvariantCulture))).ToList();
            await pdf.ExportAsync(Path.Combine(Output,"english.pdf"),rows);
            SchedulePdfRow arabic=new(1,"المرحلة الأولى","عام","A","","Theory","الرياضيات","أحمد علي","قاعة ١","Sunday","08:30 AM","09:50 AM");
            await pdf.ExportAsync(Path.Combine(Output,"arabic.pdf"),[arabic]);
            await pdf.ExportAsync(Path.Combine(Output,"mixed.pdf"),[arabic with {Subject="Programming برمجة 101",FacultyMember="د. أحمد Ali",Classroom="قاعة 01 | Lab A1"}]);
            await pdf.ExportAsync(Path.Combine(Output,"empty.pdf"),[]);
            await pdf.ExportAsync(Path.Combine(Output,"multi-page.pdf"),rows.Concat([arabic,arabic with {SemesterNumber=2}]).ToList());
            foreach(string f in new[]{"english","arabic","mixed","empty","multi-page"}) Assert(new FileInfo(Path.Combine(Output,f+".pdf")).Length>1000);
        });
        await FlexiblePeriods();
        await Check("Identity tables support atomic flexible-period add and update", IdentityPeriods);
    }
    static async Task<string> PeriodSnapshot()
    {
        await using var context=new AppDbContext();
        return JsonSerializer.Serialize(await context.TimeSlots.AsNoTracking().OrderBy(slot=>slot.TimeSlotID)
            .Select(slot=>new{slot.TimeSlotID,slot.StartTime,slot.EndTime,slot.IsBreak}).ToListAsync());
    }
    static async Task IdentityPeriods()
    {
        string? previous=Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
        var connection=new SqlConnectionStringBuilder{DataSource=TestServer,InitialCatalog="master",IntegratedSecurity=true,Encrypt=false,TrustServerCertificate=true};
        string database="UniversityTimetable_FourFixes_Identity_"+Guid.NewGuid().ToString("N")[..8];
        using(var master=new SqlConnection(connection.ConnectionString))
        {
            await master.OpenAsync();
            using var command=new SqlCommand($"CREATE DATABASE [{database}]",master);await command.ExecuteNonQueryAsync();
        }
        connection.InitialCatalog=database;
        try
        {
            Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
            await using(var context=new AppDbContext())
                await context.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.TimeSlots (TimeSlotID int IDENTITY(1,1) NOT NULL PRIMARY KEY, StartTime time NOT NULL, EndTime time NOT NULL, IsBreak bit NOT NULL, UNIQUE(StartTime,EndTime,IsBreak));");
            var service=new TimeSlotService();
            var first=await service.AddAsync(new(){StartTime=new(8,30,0),EndTime=new(9,30,0)});
            await service.AddAsync(new(){StartTime=new(10,0,0),EndTime=new(11,30,0)});
            first.EndTime=new TimeSpan(9,0,0);await service.UpdateAsync(first);
            var periods=await service.GetAllAsync();Assert(periods.Count==2 && periods.Single(slot=>slot.TimeSlotID==first.TimeSlotID).EndTime==first.EndTime);
            string before=await PeriodSnapshot();await Reject(()=>service.AddAsync(new(){StartTime=new(8,45,0),EndTime=new(9,45,0)}));Assert(await PeriodSnapshot()==before);
            Console.WriteLine("ISOLATED IDENTITY DATABASE: "+database);
        }
        finally{Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,previous);}
    }
    static async Task FlexiblePeriods()
    {
        var service=new TimeSlotService();
        TimeSlot? afternoon=null;
        await Check("A 90-minute afternoon period can be added and read",async()=>{
            afternoon=await service.AddAsync(new(){StartTime=new(13,30,0),EndTime=new(15,0,0)});
            Assert((await service.GetAllAsync()).Any(slot=>slot.TimeSlotID==afternoon.TimeSlotID));
            Assert((await service.GetByIdAsync(afternoon.TimeSlotID))!.EndTime==new TimeSpan(15,0,0));
        });
        await Check("Startup maintenance preserves all configured periods and IDs",async()=>{
            string before=await PeriodSnapshot();
            await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
            await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
            Assert(await PeriodSnapshot()==before);
        });
        await Check("A scheduled period can shorten to 60 minutes without changing lesson IDs",async()=>{
            var first=(await service.GetAllAsync()).First();
            string schedulesBefore=await Snapshot();
            first.EndTime=new TimeSpan(9,30,0);await service.UpdateAsync(first);
            Assert(await Snapshot()==schedulesBefore);
            Assert((await Schedules.GetAllAsync()).Where(s=>s.TimeSlotID==first.TimeSlotID).All(s=>s.TimeSlot.EndTime==first.EndTime));
            string before=await PeriodSnapshot();await new DatabaseMaintenanceService().ApplyPendingFixesAsync();Assert(await PeriodSnapshot()==before);
        });
        await Check("The last morning period can extend beyond the old workday",async()=>{
            var last=(await service.GetAllAsync()).Single(slot=>slot.StartTime==new TimeSpan(11,30,0));
            last.EndTime=new TimeSpan(13,20,0);await service.UpdateAsync(last);
            Assert((await service.GetByIdAsync(last.TimeSlotID))!.EndTime==last.EndTime);
        });
        await Check("An overlapping edit is rejected without changing periods or lessons",async()=>{
            string periodsBefore=await PeriodSnapshot(),schedulesBefore=await Snapshot();
            var first=(await service.GetAllAsync()).First();first.EndTime=new TimeSpan(10,10,0);
            await Reject(()=>service.UpdateAsync(first));
            Assert(await PeriodSnapshot()==periodsBefore && await Snapshot()==schedulesBefore);
        });
        await Check("Duplicate periods and gaps below ten minutes are rejected",async()=>{
            string before=await PeriodSnapshot();
            await Reject(()=>service.AddAsync(new(){StartTime=new(13,30,0),EndTime=new(15,0,0)}));
            await Reject(()=>service.AddAsync(new(){StartTime=new(15,5,0),EndTime=new(16,0,0)}));
            Assert(await PeriodSnapshot()==before);
        });
        await Check("A new 120-minute period with an exact ten-minute gap is accepted",async()=>{
            var extra=await service.AddAsync(new(){StartTime=new(15,10,0),EndTime=new(17,10,0)});
            Assert((await service.GetByIdAsync(extra.TimeSlotID))!.EndTime-extra.StartTime==TimeSpan.FromMinutes(120));
        });
        await Check("Invalid persisted times are rejected without a partial write",async()=>{
            string before=await PeriodSnapshot();
            await Reject(()=>service.AddAsync(new(){StartTime=new(20,0,0),EndTime=new(19,0,0)}));
            await Reject(()=>service.AddAsync(new(){StartTime=new(20,0,30),EndTime=new(21,0,30)}));
            Assert(await PeriodSnapshot()==before);
        });
        if(afternoon is null)return;
        await Check("Generation uses a custom-only calendar without restoring the fixed periods",async()=>{
            List<TimeSlot> removed;
            await using(var context=new AppDbContext())
            {
                context.Schedules.RemoveRange(await context.Schedules.ToListAsync());await context.SaveChangesAsync();
                removed=await context.TimeSlots.AsNoTracking().Where(slot=>slot.TimeSlotID!=afternoon.TimeSlotID).ToListAsync();
                context.TimeSlots.RemoveRange(removed);await context.SaveChangesAsync();
                foreach(var subject in await context.Subjects.ToListAsync()){subject.TheoreticalHours=1;subject.PracticalHours=0;}
                await context.SaveChangesAsync();
            }
            try
            {
                var result=await Schedules.GenerateAsync();
                Assert(result.CreatedCount==result.RequiredCount && result.CreatedCount>0 && result.TimeSlotCount==1);
                Assert((await Schedules.GetAllAsync()).All(s=>s.TimeSlotID==afternoon.TimeSlotID && s.TimeSlot.EndTime-s.TimeSlot.StartTime==TimeSpan.FromMinutes(90)));
                Assert((await service.GetAllAsync()).Count==1);
            }
            finally
            {
                await using var context=new AppDbContext();context.TimeSlots.AddRange(removed);await context.SaveChangesAsync();
            }
        });
        await Check("Legacy overlapping periods remain visible and scheduling is safely rejected",async()=>{
            var bad=new TimeSlot{TimeSlotID=999,StartTime=new(13,45,0),EndTime=new(14,30,0)};
            await using(var context=new AppDbContext()){context.TimeSlots.Add(bad);await context.SaveChangesAsync();}
            try
            {
                Assert((await service.GetAllAsync()).Any(slot=>slot.TimeSlotID==bad.TimeSlotID));
                string before=await Snapshot(),periods=await PeriodSnapshot();
                await new DatabaseMaintenanceService().ApplyPendingFixesAsync();Assert(await PeriodSnapshot()==periods);
                await Reject(()=>Schedules.GenerateAsync());Assert(await Snapshot()==before);
                var lesson=(await Schedules.GetAllAsync()).First();
                await Reject(()=>Schedules.AddAsync(new(){SubjectID=lesson.SubjectID,FacultyMemberID=lesson.FacultyMemberID,ClassroomID=lesson.ClassroomID,SectionID=lesson.SectionID,TimeSlotID=bad.TimeSlotID,DayOfWeek=lesson.DayOfWeek,LectureType=lesson.LectureType,GroupName=lesson.GroupName}));
                Assert(await Snapshot()==before);
            }
            finally{await using var context=new AppDbContext();context.TimeSlots.Remove(await context.TimeSlots.SingleAsync(slot=>slot.TimeSlotID==bad.TimeSlotID));await context.SaveChangesAsync();}
        });
        await Check("Deleting an unused initial period does not make startup recreate it",async()=>{
            var unused=(await service.GetAllAsync()).First(slot=>slot.TimeSlotID!=afternoon.TimeSlotID);
            await service.DeleteAsync(unused.TimeSlotID);string before=await PeriodSnapshot();
            await new DatabaseMaintenanceService().ApplyPendingFixesAsync();Assert(await PeriodSnapshot()==before);
        });
    }
    static IEnumerable<Control> Children(Control parent) {foreach(Control c in parent.Controls){yield return c;foreach(var d in Children(c))yield return d;}}
    static bool CanReach(Control target, Form window)
    {
        var sample=target.ClientRectangle;
        for(Control? parent=target.Parent;parent is not null;parent=parent.Parent)
        {
            if(parent is ScrollableControl scroll && scroll.AutoScroll)
            {
                var rectangle=scroll.RectangleToClient(target.RectangleToScreen(sample));
                int dx=rectangle.Left<0?rectangle.Left:Math.Max(0,rectangle.Right-scroll.ClientSize.Width);
                int dy=rectangle.Top<0?rectangle.Top:Math.Max(0,rectangle.Bottom-scroll.ClientSize.Height);
                var position=scroll.AutoScrollPosition;
                scroll.AutoScrollPosition=new Point(Math.Max(0,-position.X+dx),Math.Max(0,-position.Y+dy));
            }
        }
        var visible=target.RectangleToScreen(sample);
        for(Control? parent=target.Parent;parent is not null;parent=parent.Parent)
            if(!parent.RectangleToScreen(parent.ClientRectangle).Contains(visible))return false;
        return window.RectangleToScreen(window.ClientRectangle).Contains(visible);
    }
    static async Task Timing()
    {
        foreach (var period in new[] { (Start:new TimeSpan(8,30,0),End:new TimeSpan(9,50,0)), (Start:new TimeSpan(10,0,0),End:new TimeSpan(11,20,0)), (Start:new TimeSpan(11,30,0),End:new TimeSpan(12,50,0)), (Start:new TimeSpan(8,30,0),End:new TimeSpan(9,30,0)), (Start:new TimeSpan(13,30,0),End:new TimeSpan(15,0,0)), (Start:new TimeSpan(15,10,0),End:new TimeSpan(17,10,0)), (Start:new TimeSpan(9,0,0),End:new TimeSpan(10,20,0)), (Start:new TimeSpan(8,30,0),End:new TimeSpan(8,31,0)), (Start:new TimeSpan(23,0,0),End:new TimeSpan(23,59,0)) })
            await Check("Flexible lecture period accepted: " + period.Start + "-" + period.End, () => { TimeSlotService.ValidateLectureRange(period.Start,period.End); return Task.CompletedTask; });
        await Check("Initial lecture defaults remain 08:30 to 09:50", () =>
        {
            var period=TimeSlotService.DefaultLecturePeriod;
            TimeSlotService.ValidateLectureRange(period.Start,period.End);
            Assert(period.Start==new TimeSpan(8,30,0) && period.End==new TimeSpan(9,50,0));
            return Task.CompletedTask;
        });
        foreach (var invalid in new[] { (Name:"Reversed time range",Start:new TimeSpan(10,0,0),End:new TimeSpan(9,50,0)), (Name:"Equal start and end",Start:new TimeSpan(10,0,0),End:new TimeSpan(10,0,0)), (Name:"Non-minute precision",Start:new TimeSpan(10,0,30),End:new TimeSpan(11,20,30)), (Name:"Negative time",Start:TimeSpan.FromMinutes(-1),End:new TimeSpan(1,0,0)), (Name:"End at 24:00",Start:new TimeSpan(23,0,0),End:TimeSpan.FromDays(1)), (Name:"Next-day time",Start:TimeSpan.FromDays(1),End:TimeSpan.FromDays(1)+TimeSpan.FromHours(1)) })
            await Check(invalid.Name + " rejected", () => Reject(() => { TimeSlotService.ValidateLectureRange(invalid.Start,invalid.End); return Task.CompletedTask; }));
        await Check("Invalid-period message explains same-day and whole-minute inputs", () =>
        {
            try { TimeSlotService.ValidateLectureRange(new TimeSpan(9,0,30),new TimeSpan(10,30,30)); throw new Exception("Expected rejection"); }
            catch (ArgumentException ex) { Assert(new[]{"same day","whole minutes","flexible"}.All(ex.Message.Contains)); }
            return Task.CompletedTask;
        });
    }
    static void RecordUiException(Exception exception)
    {
        failures++;
        Results.Add(new { name = "Unhandled UI exception", passed = false, error = exception.ToString() });
        Console.WriteLine("FAIL Unhandled UI exception: " + exception);
    }
    static void WriteUiResults(string file)
    {
        File.WriteAllText(Path.Combine(Output,file),JsonSerializer.Serialize(Results,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SUMMARY: {Results.Count} checks, {failures} failures");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    static SqlConnectionStringBuilder UiConnection()
    {
        string db=File.ReadAllText(Path.Combine(Output,"database.txt")).Trim();Assert(db.StartsWith("UniversityTimetable_FourFixes_"));
        var connection=new SqlConnectionStringBuilder{DataSource=TestServer,InitialCatalog=db,IntegratedSecurity=true,Encrypt=false,TrustServerCertificate=true};
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
        return connection;
    }
    static void Ui(bool autoClose)
    {
        UiConnection();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => RecordUiException(e.Exception);
        using var shell=new MainShellForm {Text="REGRESSION - Original timetable application"};
        var show=typeof(MainShellForm).GetMethod("ShowPage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var pageType=show.GetParameters()[0].ParameterType;
        shell.Shown+=async(_,_)=>{
            try {
            var layouts=new List<object>();
            foreach(int width in new[]{1100,1280,1600,1920,1280}) {
                shell.WindowState=FormWindowState.Normal;shell.Size=new Size(width,960);
                foreach(string page in new[]{"Schedules","TimeSlots"}) {
                    show.Invoke(shell,[Enum.Parse(pageType,page)]);await Task.Delay(400);
                    var host=shell.Controls.Find("pnlPageHost",true).Single();var content=host.Controls[0];
                    var actions=Children(content).Where(c=>c.Visible && (c.Name.StartsWith("btn") && !c.Name.StartsWith("btnNavigation") || c.Name=="cmbClassroom" || c.Name=="cmbFacultyFilter")).Select(c=>new{c.Name,c.Text,inside=host.RectangleToScreen(host.ClientRectangle).Contains(c.RectangleToScreen(c.ClientRectangle)),reachable=CanReach(c,shell),c.Bounds}).ToList();
                    layouts.Add(new{page,requestedWidth=width,actualWidth=shell.Width,actualHeight=shell.Height,hostWidth=host.Width,contentWidth=content.Width,actions});
                    await Check($"{page} controls accessible at {width}px",()=>{Assert(actions.All(a=>a.reachable),string.Join(", ",actions.Where(a=>!a.reachable).Select(a=>a.Name)));return Task.CompletedTask;});
                    foreach(var scroll in Children(shell).OfType<ScrollableControl>())if(scroll.AutoScroll)scroll.AutoScrollPosition=Point.Empty;
                }
            }
            Console.WriteLine("WRITING_UI_RESULTS");
            try {File.WriteAllText(Path.Combine(Output,"ui-layout.json"),JsonSerializer.Serialize(layouts,new JsonSerializerOptions{WriteIndented=true}));}
            catch(Exception ex) {Console.WriteLine("UI_RESULT_ERROR: "+ex);}
            shell.Size=new Size(1280,960);show.Invoke(shell,[Enum.Parse(pageType,"Schedules")]);
            await Task.Delay(1000);
            if (autoClose) shell.Close();
            else Console.WriteLine("UI_READY_FOR_VISUAL_REVIEW");
            } catch (Exception ex) { RecordUiException(ex); shell.Close(); }
        };
        Application.Run(shell);
        WriteUiResults("ui-checks.json");
    }
    static void UiNavigation()
    {
        var connection = UiConnection();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => RecordUiException(e.Exception);
        using var shell = new MainShellForm { Text = "REGRESSION - Deferred loading and navigation" };
        var show = typeof(MainShellForm).GetMethod("ShowPage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var pageType = show.GetParameters()[0].ParameterType;
        shell.Shown += async (_, _) =>
        {
            var disposedAccesses = new System.Collections.Concurrent.ConcurrentQueue<string>();
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> disposedProbe = (_, e) =>
            {
                if (e.Exception is ObjectDisposedException) disposedAccesses.Enqueue(e.Exception.ToString());
            };
            AppDomain.CurrentDomain.FirstChanceException += disposedProbe;
            try
            {
                using var gate = new SqlConnection(connection.ConnectionString);
                await gate.OpenAsync();
                using var transaction = gate.BeginTransaction();
                // Read-only table locks deliberately defer real service reads in the isolated database.
                using (var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.Branches WITH (TABLOCKX,HOLDLOCK); SELECT TOP (1) 1 FROM dbo.StudyYears WITH (TABLOCKX,HOLDLOCK); SELECT TOP (1) 1 FROM dbo.Subjects WITH (TABLOCKX,HOLDLOCK); SELECT TOP (1) 1 FROM dbo.Classrooms WITH (TABLOCKX,HOLDLOCK); SELECT TOP (1) 1 FROM dbo.TimeSlots WITH (TABLOCKX,HOLDLOCK); SELECT TOP (1) 1 FROM dbo.FacultyMembers WITH (TABLOCKX,HOLDLOCK);", gate, transaction))
                    await command.ExecuteNonQueryAsync();
                var pages = new[]
                {
                    (Page:"Branches", Load:"LoadBranchesAsync"), (Page:"StudyYears", Load:"LoadStudyYearsAsync"),
                    (Page:"Sections", Load:"RefreshSectionsAsync"), (Page:"Subjects", Load:"RefreshSubjectsAsync"),
                    (Page:"Classrooms", Load:"LoadClassroomsAsync"), (Page:"TimeSlots", Load:"LoadTimeSlotsAsync"),
                    (Page:"FacultyMembers", Load:"LoadFacultyMembersAsync"), (Page:"FacultyAssignments", Load:"RefreshAssignmentsAsync"),
                    (Page:"Schedules", Load:"RefreshSchedulesAsync")
                };
                var pending = new List<(string Name, Control Page, Task Load)>();
                foreach (var item in pages)
                {
                    show.Invoke(shell,[Enum.Parse(pageType,item.Page)]);
                    var content = shell.Controls.Find("pnlPageHost",true).Single().Controls[0];
                    var load = (Task)content.GetType().GetMethod(item.Load,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(content,null)!;
                    pending.Add((item.Page,content,load));
                    await Check(item.Page + " load is actually deferred", () => { Assert(!load.IsCompleted); return Task.CompletedTask; });
                }
                show.Invoke(shell,[Enum.Parse(pageType,"Subjects")]);
                await Check("Navigation disposes all nine pages while their reads are pending", () => { Assert(pending.All(p=>p.Page.IsDisposed && !p.Load.IsCompleted)); return Task.CompletedTask; });
                transaction.Commit();
                foreach (var item in pending)
                    await Check(item.Name + " deferred load completes safely after disposal", () => item.Load.WaitAsync(TimeSpan.FromSeconds(20)));
                var livePage = shell.Controls.Find("pnlPageHost",true).Single().Controls[0];
                var liveLoad = (Task)livePage.GetType().GetMethod("RefreshSubjectsAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(livePage,null)!;
                await liveLoad.WaitAsync(TimeSpan.FromSeconds(20));
                await Task.Delay(500); // Let async-void OnLoad continuations report any errors too.
                await Check("The current page still loads and displays subjects", () =>
                {
                    var grid=Children(livePage).OfType<DataGridView>().Single();
                    Assert(!livePage.IsDisposed && grid.DataSource is not null && grid.Rows.Count>0);
                    return Task.CompletedTask;
                });
                await Check("Deferred navigation never accesses disposed controls", () => { Assert(disposedAccesses.IsEmpty,string.Join("\n",disposedAccesses)); return Task.CompletedTask; });
            }
            catch (Exception ex) { RecordUiException(ex); }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= disposedProbe;
                shell.Close();
            }
        };
        Application.Run(shell);
        WriteUiResults("navigation-checks.json");
    }
}
