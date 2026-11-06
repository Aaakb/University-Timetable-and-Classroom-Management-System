using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

string source=DatabaseConnection.ConnectionString;
var builder=new SqlConnectionStringBuilder(source) { InitialCatalog="UniversityTimetable_SemesterTest_"+Guid.NewGuid().ToString("N") };
string dbName=builder.InitialCatalog;
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,builder.ConnectionString);
try
{
    await using(var db=new AppDbContext())
    {
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new StudyYear{StudyYearID=1,YearName="First Year"});
        db.Sections.Add(new Section{SectionID=1,SectionName="A",StudyYearID=1,StudentCount=20});
        db.FacultyMembers.Add(new FacultyMember{FacultyMemberID=1,FullName="Teacher"});
        db.Classrooms.Add(new Classroom{ClassroomID=1,ClassroomNumber="Hall",RoomType="Lecture",Capacity=40});
        db.TimeSlots.Add(new TimeSlot{TimeSlotID=1,StartTime=TimeSpan.FromHours(8),EndTime=TimeSpan.FromHours(9)});
        db.Subjects.AddRange(new Subject{SubjectID=1,SubjectName="First",StudyYearID=1,SemesterNumber=1,TheoreticalHours=1},new Subject{SubjectID=2,SubjectName="Second",StudyYearID=1,SemesterNumber=2,TheoreticalHours=1});
        await db.SaveChangesAsync();
        var subject=await db.Subjects.OrderBy(s=>s.SemesterNumber).ToListAsync();
        var teacher=await db.FacultyMembers.SingleAsync();var section=await db.Sections.SingleAsync();var room=await db.Classrooms.SingleAsync();var slot=await db.TimeSlots.SingleAsync();
        db.FacultyMemberSubjects.AddRange(subject.Select(s=>new FacultyMemberSubject{FacultyMemberID=teacher.FacultyMemberID,SubjectID=s.SubjectID,LectureType="Theory"}));
        db.Schedules.AddRange(subject.Select(s=>new Schedule{SubjectID=s.SubjectID,FacultyMemberID=teacher.FacultyMemberID,SectionID=section.SectionID,StudyYearID=1,ClassroomID=room.ClassroomID,TimeSlotID=slot.TimeSlotID,DayOfWeek="Sunday",SemesterNumber=s.SemesterNumber,LectureType="Theory"}));
        await db.SaveChangesAsync();
    }
    string firstBefore;
    await using(var db=new AppDbContext()) firstBefore=JsonSerializer.Serialize(await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==1).ToListAsync());
    var result=await new ScheduleService().RegenerateSemesterAsync(2);
    if(result.CreatedCount!=1)throw new Exception("Second semester must generate one lecture");
    await using(var db=new AppDbContext())
    {
        string firstAfter=JsonSerializer.Serialize(await db.Schedules.AsNoTracking().Where(s=>s.SemesterNumber==1).ToListAsync());
        if(firstBefore!=firstAfter)throw new Exception("First semester was changed");
        if(await db.Schedules.CountAsync(s=>s.SemesterNumber==2)!=1)throw new Exception("Second semester missing");
    }
    try { await new ScheduleService().RegenerateSemesterAsync(3); throw new Exception("Invalid semester accepted"); }
    catch(ArgumentOutOfRangeException) { }
    string allBefore;
    await using(var db=new AppDbContext())
    {
        await db.FacultyMemberSubjects.Where(a=>a.SubjectID==2).ExecuteDeleteAsync();
        allBefore=JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync());
    }
    try { await new ScheduleService().RegenerateSemesterAsync(2); throw new Exception("Unassigned subject accepted"); }
    catch(ArgumentException) { }
    await using(var db=new AppDbContext())
        if(allBefore!=JsonSerializer.Serialize(await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync()))throw new Exception("Failed generation changed saved schedules");
    Console.WriteLine("PASS: scoped generation preserves the first semester, rejects invalid scope, and rolls back on failure");
}
finally
{
    if(!System.Text.RegularExpressions.Regex.IsMatch(dbName,"^UniversityTimetable_SemesterTest_[a-f0-9]{32}$"))throw new Exception("Unsafe database name");
    await using var db=new AppDbContext(); await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,source);
}
