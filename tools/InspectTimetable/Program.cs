using System.Text.Json;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

// Read-only inspection: no schema initializers, SaveChanges, or generation calls.
await using var db=new AppDbContext();
db.ChangeTracker.QueryTrackingBehavior=QueryTrackingBehavior.NoTracking;
var rows=await new ScheduleService().GetAllAsync();
var subjects=await db.Subjects.Where(x=>!x.IsDeleted).ToListAsync();
var sections=await db.Sections.ToListAsync();
var assignments=await db.FacultyMemberSubjects.ToListAsync();
var periods=await db.TimeSlots.ToListAsync();
var conflicts=new List<object>();
for(int i=0;i<rows.Count;i++)for(int j=i+1;j<rows.Count;j++){
 var a=rows[i];var b=rows[j];
 if(a.SemesterNumber!=b.SemesterNumber||a.DayOfWeek!=b.DayOfWeek||a.TimeSlot.StartTime>=b.TimeSlot.EndTime||b.TimeSlot.StartTime>=a.TimeSlot.EndTime)continue;
 bool students=a.SectionID==b.SectionID&&(string.IsNullOrWhiteSpace(a.GroupName)||string.IsNullOrWhiteSpace(b.GroupName)||string.Equals(a.GroupName,b.GroupName,StringComparison.OrdinalIgnoreCase));
 if(a.FacultyMemberID==b.FacultyMemberID||a.ClassroomID==b.ClassroomID||students)conflicts.Add(new{a=a.ScheduleID,b=b.ScheduleID,faculty=a.FacultyMemberID==b.FacultyMemberID,room=a.ClassroomID==b.ClassroomID,students});
}
var invalid=rows.Where(x=>x.Subject.IsDeleted||x.Subject.SemesterNumber!=x.SemesterNumber||x.Subject.StudyYearID!=x.StudyYearID||x.TimeSlot.IsBreak||!assignments.Any(a=>a.SubjectID==x.SubjectID&&a.FacultyMemberID==x.FacultyMemberID&&(a.SectionID==null||a.SectionID==x.SectionID)&&(a.LectureType=="Both"||a.LectureType==x.LectureType))).Select(x=>new{x.ScheduleID,x.Subject.SubjectName,x.Subject.IsDeleted,x.SemesterNumber,subjectSemester=x.Subject.SemesterNumber,assignmentValid=assignments.Any(a=>a.SubjectID==x.SubjectID&&a.FacultyMemberID==x.FacultyMemberID&&(a.SectionID==null||a.SectionID==x.SectionID)&&(a.LectureType=="Both"||a.LectureType==x.LectureType))}).ToList();
var assembly=typeof(ScheduleService).Assembly;const string prefix="University_Timetable_and_Classroom_Management_System.BusinessLayer.";
var generator=assembly.GetType(prefix+"ScheduleGenerator")!;
var teachingType=assembly.GetType(prefix+"SubjectTeachingAssignment")!;
var missing=new List<object>();var extra=new List<object>();
var expectedKeys=new HashSet<string>();
foreach(var subject in subjects.Where(x=>x.HasWeeklyLectures))foreach(var section in sections.Where(s=>s.StudyYearID==subject.StudyYearID&&AcademicStructureRules.SectionMatchesSubject(subject.StudyYearID,subject.BranchID,s.BranchID,s.SectionName))){
 var faculty=assignments.Where(x=>x.SubjectID==subject.SubjectID&&(x.SectionID==null||x.SectionID==section.SectionID)).Select(x=>x.FacultyMemberID).Distinct().ToList();
 var teaching=Activator.CreateInstance(teachingType,subject,faculty)!;
 var requests=((System.Collections.IEnumerable)generator.GetMethods().Single(m=>m.Name=="CreateScheduleRequests"&&m.GetParameters().Length==2).Invoke(null,[teaching,section])!).Cast<object>();
 foreach(var group in requests.GroupBy(x=>(Type:(string)x.GetType().GetProperty("LectureType")!.GetValue(x)!,Group:(string?)x.GetType().GetProperty("GroupName")!.GetValue(x)))){
  var matching=rows.Where(x=>x.SubjectID==subject.SubjectID&&x.SectionID==section.SectionID&&x.SemesterNumber==subject.SemesterNumber&&x.LectureType==group.Key.Type&&(string.Equals(x.GroupName??"",group.Key.Group??"",StringComparison.OrdinalIgnoreCase)||group.Key.Type=="Practical"&&string.IsNullOrWhiteSpace(x.GroupName))).ToList();
  foreach(var x in matching)expectedKeys.Add(x.ScheduleID.ToString());
  if(matching.Count!=group.Count())missing.Add(new{subject=subject.SubjectName,subject.SubjectID,section=section.SectionName,section.SectionID,subject.SemesterNumber,type=group.Key.Type,group=group.Key.Group,required=group.Count(),actual=matching.Count});
 }
}
extra.AddRange(rows.Where(x=>!expectedKeys.Contains(x.ScheduleID.ToString())).Select(x=>(object)new{x.ScheduleID,x.Subject.SubjectName,section=x.Section.SectionName,x.LectureType,x.GroupName}));
object Daily(IEnumerable<Schedule> source){var ordered=source.OrderBy(x=>x.TimeSlot.StartTime).ToList();double gaps=0;var end=ordered[0].TimeSlot.EndTime;foreach(var x in ordered.Skip(1)){gaps+=Math.Max(0,(x.TimeSlot.StartTime-end).TotalMinutes-10);if(x.TimeSlot.EndTime>end)end=x.TimeSlot.EndTime;}return new{count=ordered.Count,start=ordered[0].TimeSlot.StartTime.ToString(),end=end.ToString(),excessGapMinutes=gaps,spanMinutes=(end-ordered[0].TimeSlot.StartTime).TotalMinutes};}
var facultyDays=rows.GroupBy(x=>new{x.SemesterNumber,x.FacultyMemberID,x.FacultyMember.FullName,x.DayOfWeek}).Select(g=>new{g.Key,quality=Daily(g)}).ToList();
var studentDays=new List<object>();foreach(var section in sections){var relevant=rows.Where(x=>x.SectionID==section.SectionID).ToList();var groups=relevant.Where(x=>!string.IsNullOrWhiteSpace(x.GroupName)).Select(x=>x.GroupName!).Distinct().ToList();if(groups.Count==0)groups.Add("");foreach(var group in groups)foreach(var day in relevant.Where(x=>string.IsNullOrWhiteSpace(x.GroupName)||x.GroupName==group).GroupBy(x=>new{x.SemesterNumber,x.DayOfWeek}))studentDays.Add(new{section.SectionID,section.SectionName,section.StudyYearID,section.BranchID,group,day.Key,quality=Daily(day)});}
var report=new{generatedAt=DateTimeOffset.UtcNow,sourceDatabase=db.Database.GetDbConnection().Database,readOnly=true,counts=new{lectures=rows.Count,subjects=subjects.Count,sections=sections.Count,conflicts=conflicts.Count,invalidRows=invalid.Count,requirementMismatches=missing.Count,unexpectedRows=extra.Count},conflicts,invalidRows=invalid,requirements=missing,unexpectedRows=extra,facultyDays,studentDays,periods=periods.Select(x=>new{x.TimeSlotID,x.StartTime,x.EndTime,x.IsBreak}),duplicateLessons=rows.GroupBy(x=>new{x.SubjectID,x.SectionID,x.SemesterNumber,x.LectureType,x.GroupName,x.DayOfWeek}).Where(g=>g.Count()>1).Select(g=>new{g.Key,count=g.Count(),ids=g.Select(x=>x.ScheduleID)}),overCapacity=rows.Where(x=>x.Section.StudentCount>0&&x.Classroom.Capacity<(string.IsNullOrWhiteSpace(x.GroupName)?x.Section.StudentCount:(x.Section.StudentCount+1)/2)).Select(x=>new{x.ScheduleID,x.Classroom.ClassroomNumber,x.Classroom.Capacity,x.Section.StudentCount,x.GroupName})};
Directory.CreateDirectory("output/timetable-logic-audit");var path="output/timetable-logic-audit/current.json";await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report.counts));Console.WriteLine(Path.GetFullPath(path));
