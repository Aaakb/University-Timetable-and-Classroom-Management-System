using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;
string database="UniversityTimetable_Incremental_"+Guid.NewGuid().ToString("N");
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,$"Server=localhost;Database={database};Integrated Security=True;Encrypt=False;TrustServerCertificate=True");
async Task<List<Schedule>> Rows(){await using var db=new AppDbContext();return await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync();}
string Json(object x)=>JsonSerializer.Serialize(x);
void Assert(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
Schedule Lecture(int subject,int teacher,string day,int period)=>new(){SubjectID=subject,FacultyMemberID=teacher,DayOfWeek=day,TimeSlotID=period,ClassroomID=1,SectionID=1,StudyYearID=4,BranchID=1,SemesterNumber=2,LectureType="Theory"};
await using(var db=new AppDbContext())
{
 await db.Database.EnsureCreatedAsync();
 db.StudyYears.Add(new(){StudyYearID=4,YearName="Fourth Year"});db.Branches.Add(new(){BranchID=1,BranchName="IT"});
 db.Sections.Add(new(){SectionID=1,SectionName="A",StudentCount=30,StudyYearID=4,BranchID=1});
 db.FacultyMembers.AddRange(new FacultyMember{FacultyMemberID=1,FullName="New Teacher"},new FacultyMember{FacultyMemberID=2,FullName="Existing Teacher"});
 db.Classrooms.AddRange(new Classroom{ClassroomID=1,ClassroomNumber="Lecture",RoomType="Lecture",Capacity=60},new Classroom{ClassroomID=2,ClassroomNumber="Lab",RoomType="Lab",Capacity=30});
 for(int p=1;p<=3;p++)db.TimeSlots.Add(new(){TimeSlotID=p,StartTime=new TimeSpan(8,30,0)+TimeSpan.FromMinutes((p-1)*110),EndTime=new TimeSpan(10,10,0)+TimeSpan.FromMinutes((p-1)*110)});
 for(int id=1;id<=11;id++)
 {
  db.Subjects.Add(new(){SubjectID=id,SubjectName=id==11?"New mixed subject":"Existing "+id,StudyYearID=4,BranchID=1,SemesterNumber=2,TheoreticalHours=2,PracticalHours=id==11?2:0,CreditUnits=2});
  db.FacultyMemberSubjects.Add(new(){SubjectID=id,FacultyMemberID=id==11?1:2});
 }
 await db.SaveChangesAsync();int index=1;
 foreach(var day in new[]{"Sunday","Tuesday","Wednesday","Thursday"})
 for(int p=1;p<=(day is "Sunday" or "Tuesday"?2:3);p++)db.Schedules.Add(Lecture(index++,2,day,p));
 await db.SaveChangesAsync();
}
var before=await Rows();var ids=before.Select(s=>s.ScheduleID).ToHashSet();var service=new ScheduleService();var result=await service.GenerateAsync();var after=await Rows();
Assert(result.AddedCount==3&&result.PreservedCount==10&&result.CreatedCount==13,"New mixed subject completes on fifth teaching day with accurate added/preserved counts");
Assert(Json(before)==Json(after.Where(s=>ids.Contains(s.ScheduleID))),"All ten existing lecture IDs and placements remain unchanged");
var added=after.Where(s=>!ids.Contains(s.ScheduleID)).ToList();
Assert(added.Count==3&&added.All(s=>s.SubjectID==11&&s.FacultyMemberID==1)&&added.Any(s=>s.DayOfWeek=="Monday"),"Only the new subject is added and the previously excluded day is available");
Assert(added.Count(s=>s.LectureType=="Theory")==1&&added.Count(s=>s.LectureType=="Practical")==2&&added.Where(s=>s.LectureType=="Practical").Select(s=>s.GroupName).Distinct().Count()==2,"No lecture type or subgroup is lost or duplicated");
var validate=typeof(ScheduleService).GetMethod("ValidateAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
await using(var db=new AppDbContext())foreach(var row in after)await(Task)validate.Invoke(null,new object[]{db,row,true,true})!;
Assert(true,"Every resulting lecture passes existing business and conflict validation");
var repeat=await service.GenerateAsync();Assert(repeat.AddedCount==0&&repeat.PreservedCount==13&&Json(after)==Json(await Rows()),"Repeated generation is a no-op");
await using(var db=new AppDbContext())
{
 await db.Schedules.Where(s=>s.SubjectID==11).ExecuteDeleteAsync();
 for(int p=1;p<=3;p++)db.Schedules.Add(Lecture(1,2,"Monday",p));
 db.Schedules.AddRange(Lecture(1,2,"Sunday",3),Lecture(1,2,"Tuesday",3));
 await db.SaveChangesAsync();
}
string occupied=Json(await Rows());
try{await service.GenerateAsync();throw new Exception("Expected incomplete generation to fail");}
catch(ArgumentException error){Assert(error.Message.Contains("3 of 3 additional lessons")&&error.Message.Contains("New mixed subject"),"Failure reports actual missing lessons and subject instead of incompatible timetable totals");}
Assert(occupied==Json(await Rows()),"An impossible completion preserves the entire saved timetable");
Directory.CreateDirectory("output/incremental-generation-verification");File.WriteAllText("output/incremental-generation-verification/results.json",Json(new{Database=database,Passed=true,Checks=8}));
Console.WriteLine("SUMMARY: 8 checks, 0 failures");
