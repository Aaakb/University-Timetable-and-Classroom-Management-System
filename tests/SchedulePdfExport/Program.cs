using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

string temporary = Path.GetFullPath("tmp/pdfs/instructor-export");
Directory.CreateDirectory(temporary);
Directory.CreateDirectory("output/pdf");
var export = new SchedulePdfExportService();
var instructor = new SchedulePdfExportOptions("ورود حسن");
SchedulePdfRow row = new(1, "Third Year", "Cyber Security", "A", "-", "Theory",
    "Database Security", "ورود حسن", "قاعة 402", "Thursday", "08:30 AM", "10:10 AM");
SchedulePdfRow[] fixture = [
    row,
    row with { StudyYear="Fourth Year", Branch="Information Technology", Section="B", Subject="Cloud Computing", DayOfWeek="Tuesday", StartTime="10:20", EndTime="12:00" },
    row with { SemesterNumber=2, StudyYear="Fourth Year", Branch="Information Technology", Subject="Second Semester", DayOfWeek="Sunday", StartTime="12:10", EndTime="13:50" }
];
await export.ExportAsync(Path.Combine(temporary,"instructor-fixture.pdf"),fixture,instructor);
await export.ExportAsync(Path.Combine(temporary,"standard-fixture.pdf"),fixture);
await export.ExportAsync(Path.Combine(temporary,"empty-fixture.pdf"),[],instructor);
try
{
    await export.ExportAsync(Path.Combine(temporary,"invalid.pdf"),[row with {FacultyMember="Other instructor"}],instructor);
    throw new Exception("A mismatched instructor export must be rejected.");
}
catch(ArgumentException) {Console.WriteLine("PASS: a selected instructor cannot receive someone else's lessons.");}

// Reproduce the user's report from the live database using read-only queries.
await using var db = new AppDbContext();
var details = await db.ScheduleDetails.AsNoTracking().Where(x=>x.SemesterNumber==1 && x.FacultyMemberName=="ورود حسن").ToListAsync();
if(details.Count!=6)throw new Exception("Expected the six lessons in the supplied instructor report.");
var actual = details.Select(x=>new SchedulePdfRow(x.SemesterNumber,x.YearName,x.BranchName??"-",x.SectionName,x.GroupName??"-",x.LectureType,x.SubjectName,x.FacultyMemberName,x.ClassroomNumber,x.DayOfWeek,x.StartTime.ToString(@"hh\:mm"),x.EndTime.ToString(@"hh\:mm"))).ToList();
await export.ExportAsync(Path.GetFullPath("output/pdf/instructor-warood-hassan.pdf"),actual,instructor);
Console.WriteLine("Generated instructor, standard, empty, and actual-report PDFs for visual and text verification.");
