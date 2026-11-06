using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

var original=Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var connection=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString){InitialCatalog="UniversityTimetable_AlternativeAvailability_"+Guid.NewGuid().ToString("N")[..8]};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
async Task<string> Snapshot(){await using var db=new AppDbContext();return JsonSerializer.Serialize(new{Rows=await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync(),Prefs=await db.FacultySchedulingPreferences.AsNoTracking().OrderBy(p=>p.SemesterNumber).ThenBy(p=>p.FacultyMemberID).ToListAsync()});}
var preferences=new FacultyPreferenceService();var service=new TimetableAlternativesService();
string[] days=["Sunday","Monday","Tuesday","Wednesday","Thursday"];
FacultyScheduleRequest Profile(params string[] allowed)=>new(1,2,FacultyAvailabilityMode.Strict,allowed){UseDayPreferences=true,DayPreferences=days.Select(d=>new FacultyDaySetting(d,allowed.Contains(d)?FacultyPreferenceLevel.StronglyPreferred:FacultyPreferenceLevel.Prohibited)).ToArray()};
try
{
    await using(var db=new AppDbContext())
    {
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new(){StudyYearID=1,YearName="First Year"});
        db.Sections.Add(new(){SectionID=1,SectionName="A",StudyYearID=1,StudentCount=20});
        db.FacultyMembers.Add(new(){FacultyMemberID=1,FullName="تدريسي تجريبي"});
        db.Classrooms.Add(new(){ClassroomID=1,ClassroomNumber="Room",RoomType="Lecture",Capacity=30});
        for(int p=1;p<=3;p++)db.TimeSlots.Add(new(){TimeSlotID=p,StartTime=TimeSpan.FromMinutes(480+(p-1)*70),EndTime=TimeSpan.FromMinutes(540+(p-1)*70)});
        db.TimeSlots.Add(new(){TimeSlotID=4,StartTime=TimeSpan.FromHours(14),EndTime=TimeSpan.FromHours(15),IsBreak=true});
        // An incompatible duration must not inflate the available lecture count.
        db.TimeSlots.Add(new(){TimeSlotID=5,StartTime=TimeSpan.FromHours(16),EndTime=TimeSpan.FromHours(16.5)});
        for(int id=1;id<=5;id++)
        {
            db.Subjects.Add(new(){SubjectID=id,SubjectName="Subject "+id,StudyYearID=1,SemesterNumber=id==5?1:2,TheoreticalLectureCount=1,RequirementType="University"});
            db.FacultyMemberSubjects.Add(new(){FacultyMemberID=1,SubjectID=id,LectureType="Theory"});
            db.Schedules.Add(new(){FacultyMemberID=1,SubjectID=id,SectionID=1,StudyYearID=1,SemesterNumber=id==5?1:2,DayOfWeek=id<=3?"Sunday":"Monday",TimeSlotID=id<=3?id:1,ClassroomID=1,LectureType="Theory"});
        }
        await db.SaveChangesAsync();
    }
    var saved=await preferences.SaveAsync(Profile("Sunday"),0);
    string before=await Snapshot();
    var unavailable=await service.GenerateAsync(new(2,1,5));
    Check(unavailable.Alternatives.Count==0,"Impossible availability has no saveable alternative");
    Check(unavailable.Message.Contains("تدريسي تجريبي")&&unavailable.Message.Contains("4")&&unavailable.Message.Contains("3")&&unavailable.Message.Contains("الأحد"),"Explains faculty demand, compatible capacity and allowed days");
    Check(before==await Snapshot(),"Infeasible preview changes neither schedules nor preferences");
    await preferences.SaveAsync(Profile("Sunday","Monday"),saved.Revision);
    // The planner requires consistent lecture durations; remove only the diagnostic-only half-hour slot.
    await using(var db=new AppDbContext())await db.TimeSlots.Where(p=>p.TimeSlotID==5).ExecuteDeleteAsync();
    before=await Snapshot();
    var feasible=await service.GenerateAsync(new(2,1,5));
    Check(feasible.Alternatives.Count==1,feasible.Message);
    Check(feasible.Alternatives.Single().Lectures.Count==4&&feasible.Alternatives.Single().Lectures.All(l=>l.Day is "Sunday" or "Monday"),"Expanded days allow a complete alternative within availability");
    Check(before==await Snapshot(),"Successful preview leaves both semesters and preferences unchanged");
    var first=await service.GenerateAsync(new(1,1,5));
    Check(first.Alternatives.Count==1&&first.Alternatives.Single().Lectures.Count==1,"Semester-two restrictions do not leak into semester one");
    Console.WriteLine($"SUMMARY {checks} checks passed");
}
finally
{
    await using var db=new AppDbContext();await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,original);
}
