using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

var output=Path.GetFullPath(".tmp/SchedulingCapacity");Directory.CreateDirectory(output);
var jsonOptions=new JsonSerializerOptions{WriteIndented=true};
if(args.Contains("worker")){
    var fixture=JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(output,"fixture.json")))!;
    int semester=int.Parse(args[1]);string kind=args[2];string staff=args[3];int count=int.Parse(args[4]);
    var assignments=fixture.Assignments.GroupBy(a=>a.SubjectId).ToDictionary(g=>g.Key,g=>g.Select(a=>a.FacultyId).Order().ToList());
    var newSubjects=new List<Subject>();
    var specifications=new List<(int Semester,int Count,string Staff)>{(semester,count,staff)};
    if(args.Length>6)specifications.Add((2,int.Parse(args[5]),args[6]));
    foreach(var specification in specifications){
    var loads=fixture.Sections.Where(s=>s.StudyYearID>=3).ToDictionary(s=>s.SectionID,s=>fixture.Subjects.Where(sub=>sub.StudyYearID==s.StudyYearID&&sub.BranchID==s.BranchID&&sub.SemesterNumber==specification.Semester).Sum(sub=>(sub.TheoreticalHours>0?1:0)+(sub.PracticalHours>0?1:0)));
    for(int index=0;index<specification.Count;index++){
        int increment=kind=="Mixed"?2:1;
        var section=fixture.Sections.Where(s=>loads.ContainsKey(s.SectionID)&&loads[s.SectionID]+increment<=15).OrderBy(s=>loads[s.SectionID]).ThenBy(s=>s.StudyYearID).ThenBy(s=>s.BranchID).FirstOrDefault();
        if(section is null){Console.WriteLine(JsonSerializer.Serialize(new CaseResult(semester,kind,staff,count,false,0,0,0,"Section time capacity exhausted")));return;}
        int uniqueIndex=newSubjects.Count;
        var subject=new Subject{SubjectID=100000+uniqueIndex,SubjectName=$"Capacity Test {uniqueIndex:00}",StudyYearID=section.StudyYearID,BranchID=section.BranchID,SemesterNumber=specification.Semester,TheoreticalHours=kind=="Practical"?0:1,PracticalHours=kind=="Theory"?0:1};
        newSubjects.Add(subject);loads[section.SectionID]+=increment;
        var representative=fixture.Subjects.Where(s=>s.StudyYearID==section.StudyYearID&&s.BranchID==section.BranchID&&s.SemesterNumber==specification.Semester).OrderBy(s=>s.SubjectID).First();
        assignments[subject.SubjectID]=specification.Staff=="Dedicated"?[100000+uniqueIndex]:assignments[representative.SubjectID];
    }
    }
    var assembly=typeof(ScheduleService).Assembly;const string prefix="University_Timetable_and_Classroom_Management_System.BusinessLayer.";
    var generator=assembly.GetType(prefix+"ScheduleGenerator")!;var requestType=assembly.GetType(prefix+"ScheduleGenerationRequest")!;var teachingType=assembly.GetType(prefix+"SubjectTeachingAssignment")!;
    var requests=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(requestType))!;
    var prototypes=new List<(Subject Subject,Section Section)>();
    foreach(var subject in fixture.Subjects.Concat(newSubjects)){
        var teaching=Activator.CreateInstance(teachingType,[subject,assignments[subject.SubjectID]])!;
        foreach(var section in fixture.Sections.Where(s=>s.StudyYearID==subject.StudyYearID&&AcademicStructureRules.SectionMatchesSubject(subject.StudyYearID,subject.BranchID,s.BranchID,s.SectionName))){
            foreach(var request in (IEnumerable)generator.GetMethod("CreateScheduleRequests")!.Invoke(null,[teaching,section])!)requests.Add(request);
            prototypes.Add((subject,section));
        }
    }
    var ordered=requests.Cast<object>().OrderByDescending(r=>((Subject)requestType.GetProperty("Subject")!.GetValue(r)!).StudyYearID)
        .ThenBy(r=>((Subject)requestType.GetProperty("Subject")!.GetValue(r)!).BranchID??0)
        .ThenBy(r=>((Subject)requestType.GetProperty("Subject")!.GetValue(r)!).SemesterNumber)
        .ThenBy(r=>((Subject)requestType.GetProperty("Subject")!.GetValue(r)!).SubjectName)
        .ThenBy(r=>(int)requestType.GetProperty("LessonNumber")!.GetValue(r)!)
        .ThenBy(r=>((Section)requestType.GetProperty("Section")!.GetValue(r)!).SectionName).ToList();
    requests.Clear();foreach(var r in ordered)requests.Add(r);
    var contextType=assembly.GetType(prefix+"ScheduleBuildContext")!;var context=Activator.CreateInstance(contextType,[fixture.Rooms,fixture.Periods])!;
    contextType.GetProperty("RequirePracticalExchange")!.SetValue(context,false);
    var watch=Stopwatch.StartNew();var result=generator.GetMethod("BuildPlan")!.Invoke(null,[requests,context])!;watch.Stop();
    var rows=(List<Schedule>)result.GetType().GetProperty("Schedules")!.GetValue(result)!;
    var issues=(IReadOnlyList<string>)result.GetType().GetProperty("PracticalExchangeIssues")!.GetValue(result)!;
    int conflicts=(int)result.GetType().GetProperty("ConflictCount")!.GetValue(result)!;
    bool pass=rows.Count==requests.Count&&issues.Count==0&&conflicts==0;
    if(pass){
        var expected=prototypes.SelectMany(p=>new[]{(Type:"Theory",Group:(string?)null,Needed:p.Subject.TheoreticalHours>0),(Type:"Practical",Group:p.Section.SectionName+"1",Needed:p.Subject.PracticalHours>0),(Type:"Practical",Group:p.Section.SectionName+"2",Needed:p.Subject.PracticalHours>0)}.Where(x=>x.Needed).Select(x=>$"{p.Subject.SubjectID}/{p.Section.SectionID}/{p.Subject.SemesterNumber}/{x.Type}/{x.Group}")).Order().ToList();
        var actual=rows.Select(s=>$"{s.SubjectID}/{s.SectionID}/{s.SemesterNumber}/{s.LectureType}/{s.GroupName}").Order().ToList();
        if(!expected.SequenceEqual(actual))throw new Exception("Coverage mismatch");
        for(int i=0;i<rows.Count;i++){
            var a=rows[i];var room=fixture.Rooms.Single(r=>r.ClassroomID==a.ClassroomID);var section=fixture.Sections.Single(s=>s.SectionID==a.SectionID);
            if(room.Capacity<(a.GroupName is null?section.StudentCount:(int)Math.Ceiling(section.StudentCount/2.0)))throw new Exception("Capacity mismatch");
            if(!assignments[a.SubjectID].Contains(a.FacultyMemberID))throw new Exception("Assignment mismatch");
            for(int j=i+1;j<rows.Count;j++){
                var b=rows[j];if(a.SemesterNumber!=b.SemesterNumber||a.DayOfWeek!=b.DayOfWeek||a.TimeSlotID!=b.TimeSlotID)continue;
                if(a.ClassroomID==b.ClassroomID||a.FacultyMemberID==b.FacultyMemberID||(a.SectionID==b.SectionID&&(a.GroupName is null||b.GroupName is null||a.GroupName==b.GroupName)))throw new Exception("Booking conflict");
            }
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(new CaseResult(semester,kind,staff,count,pass,requests.Count,rows.Count,watch.ElapsedMilliseconds,string.Join("; ",issues))));return;
}
var builder=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString);string clone="UniversityTimetable_Capacity_"+Guid.NewGuid().ToString("N")[..8];
await using(var connection=new SqlConnection(builder.ConnectionString)){
    await connection.OpenAsync();string Q(string s)=>"["+s.Replace("]","]]")+"]";string L(string s)=>"N'"+s.Replace("'","''")+"'";
    async Task Sql(string s){using var command=new SqlCommand(s,connection){CommandTimeout=180};await command.ExecuteNonQueryAsync();}
    string backup,data,log;
    using(var command=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))",connection))
    using(var reader=await command.ExecuteReaderAsync()){await reader.ReadAsync();backup=Path.Combine(reader.GetString(0),clone+".bak");data=Path.Combine(reader.GetString(1),clone+".mdf");log=Path.Combine(reader.GetString(2),clone+"_log.ldf");}
    await Sql($"BACKUP DATABASE {Q(builder.InitialCatalog)} TO DISK={L(backup)} WITH COPY_ONLY,CHECKSUM");
    var files=new List<(string Name,string Type)>();using(var command=new SqlCommand($"RESTORE FILELISTONLY FROM DISK={L(backup)}",connection))using(var reader=await command.ExecuteReaderAsync())while(await reader.ReadAsync())files.Add((reader.GetString(0),reader.GetString(2)));
    if(files.Count!=2)throw new Exception("Unexpected file layout");await Sql($"RESTORE DATABASE {Q(clone)} FROM DISK={L(backup)} WITH "+string.Join(",",files.Select(f=>$"MOVE {L(f.Name)} TO {L(f.Type=="L"?log:data)}")));
}
builder.InitialCatalog=clone;Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,builder.ConnectionString);File.WriteAllText(Path.Combine(output,"database.txt"),clone);
Fixture fixtureData;await using(var context=new AppDbContext()){
    fixtureData=new(await context.Subjects.AsNoTracking().ToListAsync(),await context.Sections.AsNoTracking().ToListAsync(),await context.Classrooms.AsNoTracking().ToListAsync(),await context.TimeSlots.AsNoTracking().Where(t=>!t.IsBreak).OrderBy(t=>t.StartTime).ToListAsync(),await context.FacultyMemberSubjects.Select(a=>new Assignment(a.SubjectID,a.FacultyMemberID)).ToListAsync());
}
File.WriteAllText(Path.Combine(output,"fixture.json"),JsonSerializer.Serialize(fixtureData,jsonOptions));
Console.WriteLine($"ISOLATED FULL COPY: {clone}; {fixtureData.Subjects.Count} subjects");
var results=new List<CaseResult>();
foreach(int semester in new[]{1,2})foreach(string kind in new[]{"Mixed","Theory","Practical"})foreach(string staff in new[]{"Existing","Dedicated"}){
    int ceiling=kind switch{"Mixed"=>semester==1?5:6,"Theory"=>semester==1?17:20,_=>semester==1?8:10};
    for(int count=0;count<=ceiling+1;count++){
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(string a in new[]{"worker",semester.ToString(),kind,staff,count.ToString()})start.ArgumentList.Add(a);
        using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        var exit=process.WaitForExitAsync();
        if(await Task.WhenAny(exit,Task.Delay(30000))!=exit){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();results.Add(new(semester,kind,staff,count,false,0,0,30000,"Timeout; not a proof of infeasibility"));break;}
        string text=await stdout;string error=await stderr;if(process.ExitCode!=0)throw new Exception(error);
        var row=JsonSerializer.Deserialize<CaseResult>(text)!;results.Add(row);Console.WriteLine($"Semester {semester} {kind} {staff} +{count}: {(row.Pass?"PASS":"FAIL")} {row.Created}/{row.Required} {row.Milliseconds}ms");
        File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,jsonOptions));
        if(!row.Pass)break;
    }
}
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,jsonOptions));
Console.WriteLine("SUMMARY "+JsonSerializer.Serialize(results.GroupBy(r=>new{r.Semester,r.Kind,r.Staff}).Select(g=>new{g.Key,VerifiedAdditional=g.Where(r=>r.Pass).Max(r=>r.Additional),FirstFailure=g.FirstOrDefault(r=>!r.Pass)?.Additional}),jsonOptions));
record Assignment(int SubjectId,int FacultyId);
record Fixture(List<Subject> Subjects,List<Section> Sections,List<Classroom> Rooms,List<TimeSlot> Periods,List<Assignment> Assignments);
record CaseResult(int Semester,string Kind,string Staff,int Additional,bool Pass,int Required,int Created,long Milliseconds,string Issues);
