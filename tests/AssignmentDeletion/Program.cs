using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

string database="UniversityTimetable_AssignmentDeletion_"+Guid.NewGuid().ToString("N")[..8];
string? originalEnvironment=Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var connection=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString){InitialCatalog=database};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
Console.WriteLine("ISOLATED DATABASE "+database);
int checks=0;
void Check(string name,bool success){if(!success)throw new InvalidOperationException("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
async Task<List<Schedule>> Lectures(){await using var db=new AppDbContext();return await db.Schedules.AsNoTracking().OrderBy(x=>x.ScheduleID).ToListAsync();}
string Snapshot(IEnumerable<Schedule> rows)=>JsonSerializer.Serialize(rows.Select(x=>new{x.ScheduleID,x.SubjectID,x.FacultyMemberID,x.SectionID,x.ClassroomID,x.TimeSlotID,x.SemesterNumber,x.LectureType,x.GroupName,x.DayOfWeek,x.IsLocked}));
async Task<bool> Assigned(int faculty,int subject){await using var db=new AppDbContext();return await db.FacultyMemberSubjects.AnyAsync(x=>x.FacultyMemberID==faculty&&x.SubjectID==subject);}
async Task<Subject> Material(int id){await using var db=new AppDbContext();return await db.Subjects.AsNoTracking().SingleAsync(x=>x.SubjectID==id);}
var service=new FacultyMemberSubjectService();
try
{
    await using(var db=new AppDbContext())
    {
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new(){StudyYearID=1,YearName="First Year"});
        db.Sections.AddRange(new Section{SectionID=11,SectionName="A",StudyYearID=1,StudentCount=20},new Section{SectionID=12,SectionName="B",StudyYearID=1,StudentCount=20});
        db.Subjects.AddRange(new Subject{SubjectID=101,SubjectName="Keep this material",StudyYearID=1,SemesterNumber=1,TheoreticalLectureCount=1,PracticalLectureCount=1},
            new Subject{SubjectID=205,SubjectName="Another semester",StudyYearID=1,SemesterNumber=2},
            new Subject{SubjectID=309,SubjectName="Archived material",StudyYearID=1,SemesterNumber=1,IsDeleted=true});
        db.FacultyMembers.AddRange(Enumerable.Range(1,3).Select(id=>new FacultyMember{FacultyMemberID=id,FullName="Teacher "+id}));
        db.Classrooms.Add(new(){ClassroomID=1,ClassroomNumber="Room",RoomType="Lab",Capacity=30});
        db.TimeSlots.Add(new(){TimeSlotID=1,StartTime=TimeSpan.FromHours(8),EndTime=TimeSpan.FromHours(9)});
        db.FacultyMemberSubjects.AddRange(new FacultyMemberSubject{FacultyMemberID=1,SubjectID=101,LectureType="Both"},
            new FacultyMemberSubject{FacultyMemberID=2,SubjectID=101,SectionID=11,LectureType="Practical"},
            new FacultyMemberSubject{FacultyMemberID=1,SubjectID=205,SectionID=11,LectureType="Theory"},
            new FacultyMemberSubject{FacultyMemberID=3,SubjectID=309,SectionID=11,LectureType="Both"});
        Schedule Row(int faculty,int subject,int section,string day,string type,bool locked=false)=>new()
        {FacultyMemberID=faculty,SubjectID=subject,SectionID=section,StudyYearID=1,ClassroomID=1,TimeSlotID=1,
            SemesterNumber=subject==205?2:1,DayOfWeek=day,LectureType=type,GroupName=type=="Practical"?(section==11?"A1":"B1"):"All",IsLocked=locked};
        db.Schedules.AddRange(Row(1,101,11,"Sunday","Theory",true),Row(1,101,12,"Monday","Practical"),
            Row(2,101,11,"Tuesday","Practical"),Row(1,205,11,"Wednesday","Theory"),Row(3,309,11,"Thursday","Theory"));
        await db.SaveChangesAsync();
    }
    string subjectBefore=JsonSerializer.Serialize(await Material(101));
    string unaffected=Snapshot((await Lectures()).Where(x=>x.FacultyMemberID!=1||x.SubjectID!=101));
    await service.DeleteAsync(1,101);
    Check("Used assignment is deleted",!await Assigned(1,101));
    var remaining=await Lectures();
    Check("Both theory and practical lectures disappear across covered sections",remaining.All(x=>x.SubjectID!=101||x.FacultyMemberID!=1));
    Check("Explicit assignment deletion also removes its locked lecture",remaining.All(x=>!x.IsLocked));
    Check("Other teacher, subject and semester placements remain byte-for-byte identical",Snapshot(remaining)==unaffected);
    Check("Another teacher's assignment for the same subject remains",await Assigned(2,101));
    Check("The material remains active with all its values unchanged",JsonSerializer.Serialize(await Material(101))==subjectBefore&&!(await Material(101)).IsDeleted);
    await using(var db=new AppDbContext()) Check("Teacher records remain",await db.FacultyMembers.CountAsync()==3);

    await service.DeleteAsync(2,101);
    Check("Section-specific practical assignment and its lecture are removed",!await Assigned(2,101)&&(await Lectures()).All(x=>x.FacultyMemberID!=2||x.SubjectID!=101));
    Check("Deleting the last assignment still preserves the material",!(await Material(101)).IsDeleted);
    await service.AddAsync(new(){FacultyMemberID=1,SubjectID=101,SectionID=11,LectureType="Theory"});
    Check("The remaining material can be assigned again",await Assigned(1,101));
    await service.DeleteAsync(1,101);
    Check("An assignment without lectures can still be deleted",!await Assigned(1,101));

    await service.DeleteAsync(3,309);
    Check("Archived-subject assignment also deletes its associated timetable rows",!await Assigned(3,309)&&(await Lectures()).All(x=>x.SubjectID!=309));
    Check("Archived subject itself is retained with its original archive flag",(await Material(309)).IsDeleted);

    await using(var db=new AppDbContext())
    {
        // A legacy row without an assignment must not be deleted by a not-found request.
        db.Schedules.Add(new(){FacultyMemberID=3,SubjectID=205,SectionID=11,StudyYearID=1,ClassroomID=1,TimeSlotID=1,
            SemesterNumber=2,DayOfWeek="Sunday",LectureType="Theory",GroupName="All"});
        await db.SaveChangesAsync();
    }
    string beforeFailure=Snapshot(await Lectures());
    await using(var db=new AppDbContext())
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.AssignmentDeletionBlocker (FacultyMemberID int NOT NULL, SubjectID int NOT NULL, CONSTRAINT FK_TestAssignmentDeletion FOREIGN KEY (FacultyMemberID,SubjectID) REFERENCES dbo.FacultyMemberSubjects(FacultyMemberID,SubjectID)); INSERT INTO dbo.AssignmentDeletionBlocker VALUES (1,205)");
    bool failed=false;
    try {await service.DeleteAsync(1,205);}
    catch(DbUpdateException e) when(e.InnerException is SqlException sql&&sql.Number==547&&sql.Message.Contains("FK_TestAssignmentDeletion",StringComparison.Ordinal)){failed=true;}
    finally {await using var db=new AppDbContext();await db.Database.ExecuteSqlRawAsync("DROP TABLE dbo.AssignmentDeletionBlocker");}
    Check("Injected assignment-write failure is reported",failed);
    Check("Assignment remains after the failed operation",await Assigned(1,205));
    Check("Previously deleted timetable rows roll back with the failed assignment",Snapshot(await Lectures())==beforeFailure);
    Check("Material survives rollback",!(await Material(205)).IsDeleted);
    bool missing=false;
    try {await service.DeleteAsync(3,205);}catch(KeyNotFoundException){missing=true;}
    Check("Missing assignment reports not found",missing);
    Check("Missing assignment cannot delete legacy orphan rows or another teacher's timetable",Snapshot(await Lectures())==beforeFailure);
    await new SubjectService().DeleteAsync(101);
    Check("After deleting assignments, the user can separately delete the material from its list",(await Material(101)).IsDeleted);
    Check("Deleting that material does not change another material",!(await Material(205)).IsDeleted);
    Console.WriteLine($"SUMMARY {checks} checks; 0 failures");
}
finally
{
    // Only this randomly named test database is eligible for cleanup.
    if(connection.InitialCatalog!=database||!database.StartsWith("UniversityTimetable_AssignmentDeletion_",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe test cleanup target");
    await using(var db=new AppDbContext())await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,originalEnvironment);
}
