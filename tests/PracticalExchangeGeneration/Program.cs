using System.Collections;
using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

string original=DatabaseConnection.ConnectionString;
string name="UniversityTimetable_ExchangeTest_"+Guid.NewGuid().ToString("N");
var connection=new SqlConnectionStringBuilder(original){InitialCatalog=name};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
const string ns="University_Timetable_and_Classroom_Management_System.BusinessLayer.";
var assembly=typeof(ScheduleService).Assembly;
void Assert(bool value,string message){if(!value)throw new Exception(message);}
void CheckExchange(List<Schedule> rows)
{
    var units=rows.Where(s=>s.SemesterNumber==2).GroupBy(s=>s.SubjectID).OrderBy(g=>g.Key).ToList();
    Assert(units.Count==2 && units.All(g=>g.Count()==2),"Both subjects need both groups");
    var a=units[0].OrderBy(s=>s.GroupName).ToArray();var b=units[1].OrderBy(s=>s.GroupName).ToArray();
    Assert(rows.Where(s=>s.SemesterNumber==2).Select(s=>s.DayOfWeek).Distinct().Count()==1,"An exchange must occupy one day");
    Assert(Math.Abs(a[0].TimeSlotID-a[1].TimeSlotID)==1,"Exchange periods must be adjacent");
    Assert(a[0].TimeSlotID==b[1].TimeSlotID && a[1].TimeSlotID==b[0].TimeSlotID,"The groups must swap subjects");
    Assert(a[0].ClassroomID==a[1].ClassroomID && b[0].ClassroomID==b[1].ClassroomID,"Each teacher stays in one lab");
}
try
{
    await using(var db=new AppDbContext())
    {
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new(){StudyYearID=1,YearName="First Year"});
        db.Sections.Add(new(){SectionID=1,SectionName="A",StudyYearID=1,StudentCount=30});
        db.FacultyMembers.AddRange(new FacultyMember{FacultyMemberID=1,FullName="One"},new FacultyMember{FacultyMemberID=2,FullName="Two"});
        db.Classrooms.AddRange(new Classroom{ClassroomID=1,ClassroomNumber="Lab1",RoomType="Lab",Capacity=40},new Classroom{ClassroomID=2,ClassroomNumber="Lab2",RoomType="Lab",Capacity=40},new Classroom{ClassroomID=3,ClassroomNumber="Hall",RoomType="Lecture",Capacity=40});
        for(int p=1;p<=3;p++)db.TimeSlots.Add(new(){TimeSlotID=p,StartTime=TimeSpan.FromMinutes(510+(p-1)*90),EndTime=TimeSpan.FromMinutes(590+(p-1)*90)});
        db.Subjects.AddRange(new Subject{SubjectID=1,SubjectName="Practical one",StudyYearID=1,SemesterNumber=2,PracticalHours=2},new Subject{SubjectID=2,SubjectName="Practical two",StudyYearID=1,SemesterNumber=2,PracticalHours=2},new Subject{SubjectID=3,SubjectName="First semester",StudyYearID=1,SemesterNumber=1,TheoreticalHours=2});
        db.FacultyMemberSubjects.AddRange(new FacultyMemberSubject{SubjectID=1,FacultyMemberID=1,LectureType="Practical"},new FacultyMemberSubject{SubjectID=2,FacultyMemberID=2,LectureType="Practical"},new FacultyMemberSubject{SubjectID=3,FacultyMemberID=1,LectureType="Theory"});
        await db.SaveChangesAsync();
        db.Schedules.Add(new(){SubjectID=3,FacultyMemberID=1,ClassroomID=3,TimeSlotID=1,DayOfWeek="Sunday",SectionID=1,StudyYearID=1,SemesterNumber=1,LectureType="Theory"});
        await db.SaveChangesAsync();
    }
    await using(var db=new AppDbContext())
    {
        var subjects=await db.Subjects.AsNoTracking().Where(s=>s.SemesterNumber==2).OrderBy(s=>s.SubjectID).ToListAsync();
        var assignments=await db.FacultyMemberSubjects.AsNoTracking().ToListAsync();
        var section=await db.Sections.AsNoTracking().SingleAsync();
        var reqType=assembly.GetType(ns+"ScheduleGenerationRequest")!;
        var requests=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(reqType))!;
        var create=assembly.GetType(ns+"ScheduleGenerator")!.GetMethods().Single(m=>m.Name=="CreateScheduleRequests" && m.GetParameters().Length==3);
        foreach(var subject in subjects)foreach(var request in (IEnumerable)create.Invoke(null,[subject,section,assignments])!)requests.Add(request);
        var seeds=new List<Schedule>();
        foreach(int s in new[]{1,2})foreach(int g in new[]{1,2})seeds.Add(new(){SubjectID=s,FacultyMemberID=s,ClassroomID=s,TimeSlotID=s,DayOfWeek=g==1?"Sunday":"Monday",SectionID=1,StudyYearID=1,SemesterNumber=2,LectureType="Practical",GroupName="A"+g});
        var catalogTask=(Task)assembly.GetType(ns+"ImportCatalog")!.GetMethod("LoadAsync")!.Invoke(null,[db,CancellationToken.None])!;await catalogTask;
        var catalog=catalogTask.GetType().GetProperty("Result")!.GetValue(catalogTask);
        var solve=(Task)assembly.GetType(ns+"FreshTimetablePlanner")!.GetMethod("CompleteAsync",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[requests,new List<Schedule>(),seeds,catalog])!;
        await solve;var result=solve.GetType().GetProperty("Result")!.GetValue(solve)!;
        var rows=(List<Schedule>?)result.GetType().GetProperty("Schedules")!.GetValue(result);
        Assert(rows is not null,"Fallback should find a complete exchange");CheckExchange(rows!);
        Console.WriteLine("PASS fallback cannot keep a non-exchanging seed timetable");
    }
    string first;
    await using(var db=new AppDbContext())first=JsonSerializer.Serialize(await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==1).ToListAsync());
    await new ScheduleService().RegenerateSemesterAsync(2);
    await using(var db=new AppDbContext())
    {
        CheckExchange(await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==2).ToListAsync());
        Assert(first==JsonSerializer.Serialize(await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==1).ToListAsync()),"First semester changed");
    }
    Console.WriteLine("PASS fresh semester generation saves exchanges and preserves semester one");
    await using(var db=new AppDbContext())
    {
        db.FacultyMembers.Add(new(){FacultyMemberID=3,FullName="Merged teacher"});
        db.Subjects.Add(new(){SubjectID=4,SubjectName="Merged practical",StudyYearID=1,SemesterNumber=2,PracticalHours=2,MergePracticalGroups=true});
        db.FacultyMemberSubjects.Add(new(){SubjectID=4,FacultyMemberID=3,LectureType="Practical"});
        await db.SaveChangesAsync();
    }
    await new ScheduleService().RegenerateSemesterAsync(2);
    await using(var db=new AppDbContext())
    {
        var rows=await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==2).ToListAsync();
        CheckExchange(rows.Where(s=>s.SubjectID!=4).ToList());
        var merged=rows.Where(s=>s.SubjectID==4).ToList();
        Assert(merged.Count==1 && merged[0].GroupName is null,"Merged practical was split into groups");
        Assert(first==JsonSerializer.Serialize(await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==1).ToListAsync()),"First semester changed");
        await new ScheduleService().UpdateAsync(merged[0]);
    }
    Console.WriteLine("PASS merged practical stays whole, editable, and outside exchanges");
    var matches=typeof(TimetableBackupService).GetMethod("ReferenceMatches",BindingFlags.Static|BindingFlags.NonPublic)!;
    object Reference(string values)=>Activator.CreateInstance(assembly.GetType(ns+"TimetableBackupReference")!,["Subjects","1",values])!;
    var legacy=Reference("{\"SubjectID\":1}");
    Assert((bool)matches.Invoke(null,[legacy,Reference("{\"SubjectID\":1,\"MergePracticalGroups\":false}")])!,"Legacy backup should accept the default grouping");
    Assert(!(bool)matches.Invoke(null,[legacy,Reference("{\"SubjectID\":1,\"MergePracticalGroups\":true}")])!,"Legacy backup must not overwrite changed grouping");
    Console.WriteLine("PASS legacy backups remain compatible only with unchanged grouping");
    string beforeFailure;
    await using(var db=new AppDbContext())
    {
        await db.FacultyMemberSubjects.Where(a=>a.SubjectID==2).ExecuteUpdateAsync(u=>u.SetProperty(a=>a.FacultyMemberID,1));
        beforeFailure=JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync());
    }
    bool rejected=false;
    try {await new ScheduleService().RegenerateSemesterAsync(2);}catch(ArgumentException){rejected=true;}
    Assert(rejected,"An impossible exchange must not silently fall back to separated groups");
    await using(var db=new AppDbContext())Assert(beforeFailure==JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync()),"Failed exchange replaced saved schedules");
    Console.WriteLine("PASS impossible exchange keeps both semesters unchanged");
}
finally
{
    if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^UniversityTimetable_ExchangeTest_[a-f0-9]{32}$"))throw new Exception("Unsafe test database");
    await using var db=new AppDbContext();await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,original);
}
