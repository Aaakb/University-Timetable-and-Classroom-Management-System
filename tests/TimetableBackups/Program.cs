using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    static readonly string output=Path.GetFullPath("output/timetable-backup-verification");
    static readonly List<object> checks=[];
    static Func<AppDbContext> factory=null!;
    static TimetableBackupService service=null!;
    static readonly Fault fault=new();
    static List<Schedule> original=[];
    static int failures;
    static string sourceFile="";
    [STAThread]static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--save-current")
        {
            var current=new TimetableBackupService();var info=current.SaveAsync(args[1]).GetAwaiter().GetResult();
            var preview=current.PreviewRestoreAsync(args[1]).GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(new{info,preview.CanRestore,preview.Issues}));return;
        }
        Directory.CreateDirectory(output);Run().GetAwaiter().GetResult();
        Check("Designer contains both backup and restore buttons with responsive placement",()=>
        {
            using var form=new SchedulesForm();
            foreach(string name in new[]{"btnBackupTimetable","btnRestoreTimetable"})
            {
                var control=form.Controls.Find(name,true).Single();Assert(control is Guna.UI2.WinForms.Guna2Button);
                Assert(control.Parent?.Name=="pnlScheduleCommands");
            }
            var parent=form.Controls.Find("pnlScheduleCommands",true).Single();
            foreach(int width in new[]{420,760,1136})
            {
                parent.Width=width;
                typeof(SchedulesForm).GetMethod("LayoutScheduleButtons",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,new object[]{parent});
                var buttons=parent.Controls.OfType<Guna.UI2.WinForms.Guna2Button>().ToList();
                foreach(var button in buttons) Assert(parent.ClientRectangle.Contains(button.Bounds),"Clipped button: "+button.Name);
                for(int i=0;i<buttons.Count;i++)for(int j=i+1;j<buttons.Count;j++)Assert(!buttons[i].Bounds.IntersectsWith(buttons[j].Bounds),"Overlapping action buttons");
            }
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{Count=checks.Count,Failures=failures,Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SUMMARY: {checks.Count} checks, {failures} failures");Environment.ExitCode=failures==0?0:1;
    }
    static void Assert(bool pass,string message="Assertion failed"){if(!pass)throw new Exception(message);}
    static async Task Check(string name,Func<Task> action)
    {
        try{await action();checks.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
        catch(Exception e){failures++;checks.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}
        finally{fault.Enabled=false;}
    }
    static async Task Reject(Func<Task> operation)
    {try{await operation();}catch(Exception e)when(e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or OperationCanceledException){return;}throw new Exception("Expected rejection");}
    static async Task<List<Schedule>> Rows(){await using var db=factory();return await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync();}
    static string Json(object value)=>JsonSerializer.Serialize(value);
    static string Safety()=>Path.Combine(output,"BeforeRestore-"+Guid.NewGuid().ToString("N")+".uttbackup.json");
    static async Task Move()
    {await using var db=factory();await db.Schedules.Where(s=>s.ScheduleID==original[0].ScheduleID).ExecuteUpdateAsync(set=>set.SetProperty(s=>s.DayOfWeek,"Tuesday"));}
    static async Task Reset()
    {fault.Enabled=false;var preview=await service.PreviewRestoreAsync(sourceFile);Assert(preview.CanRestore,string.Join(";",preview.Issues));await service.RestoreAsync(preview,true,Safety());}
    static async Task Run()
    {
        string name="UniversityTimetable_Backups_"+Guid.NewGuid().ToString("N");
        string connection=$"Server=localhost;Database={name};Integrated Security=True;Encrypt=False;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection);
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).AddInterceptors(fault).Options;
        factory=()=>new AppDbContext(options);service=new(factory);
        await using(var db=factory())
        {
            await db.Database.EnsureCreatedAsync();
            db.StudyYears.Add(new(){StudyYearID=1,YearName="First Year"});
            db.Sections.Add(new(){SectionID=1,SectionName="A",StudentCount=30,StudyYearID=1});
            db.FacultyMembers.Add(new(){FacultyMemberID=1,FullName="د. أحمد"});
            db.Classrooms.Add(new(){ClassroomID=1,ClassroomNumber="قاعة 301",Capacity=60,RoomType="Lecture"});
            db.TimeSlots.Add(new(){TimeSlotID=1,StartTime=new(8,30,0),EndTime=new(10,10,0)});
            for(int semester=1;semester<=2;semester++)
            {
                db.Subjects.Add(new(){SubjectID=semester,SubjectName="مادة "+semester,StudyYearID=1,SemesterNumber=semester,TheoreticalHours=2,CreditUnits=2});
                db.FacultyMemberSubjects.Add(new(){SubjectID=semester,FacultyMemberID=1});
            }
            await db.SaveChangesAsync();
            for(int semester=1;semester<=2;semester++)db.Schedules.Add(new(){SubjectID=semester,FacultyMemberID=1,ClassroomID=1,TimeSlotID=1,DayOfWeek="Sunday",SectionID=1,StudyYearID=1,SemesterNumber=semester,IsLocked=semester==2});
            await db.SaveChangesAsync();
        }
        original=await Rows();sourceFile=Path.Combine(output,"نسخة الجدول.uttbackup.json");
        await Check("Backup saves both semesters and Arabic references without database writes",async()=>
        {
            var info=await service.SaveAsync(sourceFile);Assert(info.LectureCount==2&&info.SemesterOneCount==1&&info.SemesterTwoCount==1);
            Assert(Json(original)==Json(await Rows()));var preview=await service.PreviewRestoreAsync(sourceFile);Assert(preview.CanRestore&&preview.LectureCount==2);
        });
        await Check("Restore keeps original IDs, placements, lock metadata and creates a restorable safety backup",async()=>
        {
            await Move();string moved=Json(await Rows());var preview=await service.PreviewRestoreAsync(sourceFile);
            string safety=Safety();await service.RestoreAsync(preview,true,safety);Assert(Json(original)==Json(await Rows()));
            var undo=await service.PreviewRestoreAsync(safety);Assert(undo.CanRestore);await service.RestoreAsync(undo,true,Safety());Assert(moved==Json(await Rows()));await Reset();
        });
        await Check("Refusing confirmation leaves both timetable and filesystem unchanged",async()=>
        {
            await Move();string before=Json(await Rows());string safety=Safety();var p=await service.PreviewRestoreAsync(sourceFile);
            await Reject(()=>service.RestoreAsync(p,false,safety));Assert(before==Json(await Rows())&&!File.Exists(safety));await Reset();
        });
        await Check("Stale preview rejects a timetable changed by another operation",async()=>
        {
            var p=await service.PreviewRestoreAsync(sourceFile);await Move();string before=Json(await Rows());await Reject(()=>service.RestoreAsync(p,true,Safety()));Assert(before==Json(await Rows()));await Reset();
        });
        await Check("Changed referenced resource blocks restoration without overwriting its values",async()=>
        {
            await using(var db=factory())await db.Classrooms.Where(x=>x.ClassroomID==1).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.ClassroomNumber,"Renamed room"));
            try{var p=await service.PreviewRestoreAsync(sourceFile);Assert(!p.CanRestore&&p.Issues.Any(x=>x.Contains("Classrooms")));await Reject(()=>service.RestoreAsync(p,true,Safety()));}
            finally{await using var db=factory();await db.Classrooms.Where(x=>x.ClassroomID==1).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.ClassroomNumber,"قاعة 301"));}
        });
        await Check("Additional new subjects do not block restoring an older timetable and are never deleted",async()=>
        {
            await using(var db=factory()){db.Subjects.Add(new(){SubjectID=3,SubjectName="New subject",StudyYearID=1,SemesterNumber=1,TheoreticalHours=2});await db.SaveChangesAsync();}
            await Move();var p=await service.PreviewRestoreAsync(sourceFile);Assert(p.CanRestore);await service.RestoreAsync(p,true,Safety());
            await using var check=factory();Assert(await check.Subjects.AnyAsync(s=>s.SubjectID==3));Assert(Json(original)==Json(await Rows()));
        });
        await Check("Missing used references reject an otherwise readable backup",async()=>
        {
            await using(var db=factory())await db.FacultyMemberSubjects.Where(x=>x.SubjectID==2).ExecuteDeleteAsync();
            try{var p=await service.PreviewRestoreAsync(sourceFile);Assert(!p.CanRestore&&p.Issues.Count>0);}
            finally{await using var db=factory();db.FacultyMemberSubjects.Add(new(){SubjectID=2,FacultyMemberID=1});await db.SaveChangesAsync();}
        });
        await Check("Checksum detects edited or truncated backup files",async()=>
        {
            string changed=Path.Combine(output,"tampered.uttbackup.json");var node=JsonNode.Parse(await File.ReadAllTextAsync(sourceFile))!;
            node["Payload"]!["Lectures"]![0]!["DayOfWeek"]="Thursday";await File.WriteAllTextAsync(changed,node.ToJsonString());await Reject(()=>service.PreviewRestoreAsync(changed));
            await File.WriteAllTextAsync(changed,"{broken");await Reject(()=>service.PreviewRestoreAsync(changed));Assert(Json(original)==Json(await Rows()));
        });
        await Check("Failure after deleting current rows rolls back the complete database and retains safety backup",async()=>
        {
            await Move();string before=Json(await Rows());var p=await service.PreviewRestoreAsync(sourceFile);string safety=Safety();fault.Enabled=true;
            try{await Reject(()=>service.RestoreAsync(p,true,safety));}finally{fault.Enabled=false;}
            Assert(before==Json(await Rows())&&File.Exists(safety));await Reset();
        });
        await Check("Safety backup write failure aborts before changing a single lecture",async()=>
        {
            await Move();string before=Json(await Rows());var p=await service.PreviewRestoreAsync(sourceFile);
            string occupied=Safety();await File.WriteAllTextAsync(occupied,"preserve this file");await Reject(()=>service.RestoreAsync(p,true,occupied));
            Assert(before==Json(await Rows())&&await File.ReadAllTextAsync(occupied)=="preserve this file");await Reset();
        });
        await Check("Cancellation never commits a partial restore",async()=>
        {
            await Move();string before=Json(await Rows());var p=await service.PreviewRestoreAsync(sourceFile);using var cancel=new CancellationTokenSource();cancel.Cancel();
            await Reject(()=>service.RestoreAsync(p,true,Safety(),cancel.Token));Assert(before==Json(await Rows()));await Reset();
        });
        await Check("Empty current timetable can be restored from a complete backup",async()=>
        {
            await using(var db=factory())await db.Schedules.ExecuteDeleteAsync();var p=await service.PreviewRestoreAsync(sourceFile);Assert(p.CanRestore&&p.CurrentLectureCount==0);
            await service.RestoreAsync(p,true,Safety());Assert(Json(original)==Json(await Rows()));
        });
        File.WriteAllText(Path.Combine(output,"isolated-database.txt"),name);
    }
    sealed class Fault:SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken token=default)
        {if(Enabled)throw new InvalidOperationException("Injected restore write failure");return base.SavingChangesAsync(data,result,token);}
    }
}
