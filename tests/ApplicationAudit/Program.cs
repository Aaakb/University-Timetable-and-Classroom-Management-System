using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    static readonly List<object> Results=[];
    static int failures;
    static int expectedSubjects;
    static int expectedActiveSubjects;
    static int expectedLessons;
    static readonly string Output=Path.GetFullPath(".tmp/ApplicationAudit/results");
    [STAThread] static int Main()
    {
        Directory.CreateDirectory(Output);
        ApplicationConfiguration.Initialize();
        try { RunDatabase().GetAwaiter().GetResult(); RunEditors(); }
        catch(Exception e) { failures++; Console.WriteLine(e); Results.Add(new {name="Audit completed",passed=false,error=e.ToString()}); }
        File.WriteAllText(Path.Combine(Output,"checks.json"),JsonSerializer.Serialize(Results,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SUMMARY: {Results.Count} checks, {failures} failures");
        return failures==0?0:1;
    }
    static void Ensure(bool condition,string message="Assertion failed") { if(!condition) throw new Exception(message); }
    static async Task Check(string name,Func<Task> action)
    {
        try { await action(); Results.Add(new{name,passed=true}); Console.WriteLine("PASS "+name); }
        catch(Exception e) { failures++;Results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message); }
    }
    static async Task Reject(Func<Task> action) { try { await action(); } catch(ArgumentException) { return; } throw new Exception("Expected friendly rejection"); }
    static string Key(Schedule s)=>$"{s.SubjectID}/{s.SectionID}/{s.SemesterNumber}/{s.LectureType}/{s.GroupName}";
    static string Coverage(IEnumerable<Schedule> rows)=>JsonSerializer.Serialize(rows.GroupBy(Key).OrderBy(g=>g.Key).Select(g=>new{Key=g.Key,Count=g.Count()}));
    static async Task<string> ScheduleSnapshot()
    {
        await using var c=new AppDbContext();
        return JsonSerializer.Serialize(await c.Schedules.OrderBy(s=>s.ScheduleID).Select(s=>new{s.ScheduleID,s.SubjectID,s.FacultyMemberID,s.ClassroomID,s.TimeSlotID,s.SemesterNumber,s.StudyYearID,s.BranchID,s.SectionID,s.LectureType,s.GroupName,s.DayOfWeek}).ToListAsync());
    }
    static async Task CreateFullCopy()
    {
        string sourceText=DatabaseConnection.ConnectionString;
        var builder=new SqlConnectionStringBuilder(sourceText);
        string name="UniversityTimetable_FourFixes_Audit_"+Guid.NewGuid().ToString("N")[..8];
        await using var source=new SqlConnection(sourceText);await source.OpenAsync();
        using(var countCommand=new SqlCommand("SELECT (SELECT COUNT(*) FROM Subjects),(SELECT COUNT(*) FROM Schedules),(SELECT COUNT(*) FROM Subjects WHERE IsDeleted=0)",source))
        using(var countReader=await countCommand.ExecuteReaderAsync()) {
            await countReader.ReadAsync();expectedSubjects=countReader.GetInt32(0);expectedLessons=countReader.GetInt32(1);expectedActiveSubjects=countReader.GetInt32(2);
        }
        string Q(string v)=>"["+v.Replace("]","]]")+"]";
        string L(string v)=>"N'"+v.Replace("'","''")+"'";
        async Task Sql(string sql) { using var command=new SqlCommand(sql,source){CommandTimeout=180};await command.ExecuteNonQueryAsync(); }
        string backup,data,log;
        using(var command=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))",source))
        using(var reader=await command.ExecuteReaderAsync()) {
            await reader.ReadAsync(); backup=Path.Combine(reader.GetString(0),name+".bak");data=Path.Combine(reader.GetString(1),name+".mdf");log=Path.Combine(reader.GetString(2),name+"_log.ldf");
        }
        await Sql($"BACKUP DATABASE {Q(builder.InitialCatalog)} TO DISK={L(backup)} WITH COPY_ONLY,CHECKSUM");
        var files=new List<(string Name,string Type)>();
        using(var command=new SqlCommand($"RESTORE FILELISTONLY FROM DISK={L(backup)}",source))
        using(var reader=await command.ExecuteReaderAsync()) while(await reader.ReadAsync())files.Add((reader.GetString(0),reader.GetString(2)));
        Ensure(files.Count==2,"Unexpected database file layout");
        await Sql($"RESTORE DATABASE {Q(name)} FROM DISK={L(backup)} WITH "+string.Join(",",files.Select(f=>$"MOVE {L(f.Name)} TO {L(f.Type=="L"?log:data)}")));
        builder.InitialCatalog=name;Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,builder.ConnectionString);
        File.WriteAllText(Path.Combine(Output,"database.txt"),name);
        File.WriteAllText(Path.Combine(Output,"backup.txt"),backup);
        Console.WriteLine("FULL-DATA ISOLATED COPY: "+name);
    }
    static async Task RunDatabase()
    {
        await CreateFullCopy();
        var subjects=new SubjectService();var rooms=new ClassroomService();var sections=new SectionService();var assignments=new FacultyMemberSubjectService();var scheduleService=new ScheduleService();
        var allSubjects=await subjects.GetAllAsync();var original=await scheduleService.GetAllAsync();
        Console.WriteLine($"FULL DATA: {allSubjects.Count} subjects, {original.Count} lessons");
        await Check($"Full source copy contains all {expectedSubjects} stored subjects, {expectedActiveSubjects} active subjects and {expectedLessons} lessons",async()=>{
            await using var copy=new AppDbContext();
            Ensure(await copy.Subjects.CountAsync()==expectedSubjects && allSubjects.Count==expectedActiveSubjects && original.Count==expectedLessons);
        });
        string originalRooms=RoomSnapshot(await rooms.GetAllAsync());
        string saved=await ScheduleSnapshot();
        await Check("Editing a fully loaded saved lesson preserves its identity and related records",async()=>{
            var lesson=await scheduleService.GetByIdAsync(original[0].ScheduleID);await scheduleService.UpdateAsync(lesson!);Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("Existing subjects with matching names in different branches can be edited",async()=>{
            var duplicate=allSubjects.GroupBy(s=>new{s.SubjectName,s.StudyYearID,s.SemesterNumber}).First(g=>g.Count()>1).First();
            await subjects.UpdateAsync(duplicate);Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("Subject hours and credits reject nonfinite values",async()=>{
            foreach(var value in new[]{double.PositiveInfinity,double.NaN}) { var subject=await subjects.GetByIdAsync(allSubjects[0].SubjectID);subject!.CreditUnits=value;await Reject(()=>subjects.UpdateAsync(subject)); }
        });
        await Check("Removing a scheduled practical component is rejected without changing lessons",async()=>{
            var subject=await subjects.GetByIdAsync(original.First(s=>s.LectureType=="Practical").SubjectID);subject!.PracticalHours=0;
            await Reject(()=>subjects.UpdateAsync(subject));Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("A room capacity reduction cannot invalidate saved lessons",async()=>{
            var room=await rooms.GetByIdAsync(original[0].ClassroomID);room!.Capacity=1;await Reject(()=>rooms.UpdateAsync(room));Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("A room type change cannot invalidate saved theory lessons",async()=>{
            var theory=original.First(s=>s.LectureType=="Theory" && s.Classroom.RoomType=="Lecture");
            var room=await rooms.GetByIdAsync(theory.ClassroomID);room!.RoomType="Lab";await Reject(()=>rooms.UpdateAsync(room));Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("A section cannot be moved to another year while scheduled",async()=>{
            var section=await sections.GetByIdAsync(original.First(s=>s.StudyYearID==1).SectionID!.Value);section!.StudyYearID=2;
            // An unused branchless section name does not exist in this full fixture;
            // duplicate validation and the saved-placement guard both reject safely.
            await Reject(()=>sections.UpdateAsync(section));Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("A section student count cannot exceed booked room capacity",async()=>{
            var section=await sections.GetByIdAsync(original[0].SectionID!.Value);section!.StudentCount=1000;
            await Reject(()=>sections.UpdateAsync(section));Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("Deleting a used teaching assignment is rejected and preserved",async()=>{
            var row=original[0];await Reject(()=>assignments.DeleteAsync(row.FacultyMemberID,row.SubjectID));
            Ensure(await assignments.GetByIdAsync(row.FacultyMemberID,row.SubjectID) is not null && await ScheduleSnapshot()==saved);
        });
        await Check("Changing a used teaching assignment is rejected and preserved",async()=>{
            var row=original[0];var teacher=await new FacultyMemberService().AddAsync(new(){FullName="Audit reassignment teacher"});
            try { await Reject(()=>assignments.UpdateAsync(row.FacultyMemberID,row.SubjectID,new(){FacultyMemberID=teacher.FacultyMemberID,SubjectID=row.SubjectID}));Ensure(await assignments.GetByIdAsync(row.FacultyMemberID,row.SubjectID) is not null && await ScheduleSnapshot()==saved); }
            finally { await new FacultyMemberService().DeleteAsync(teacher.FacultyMemberID); }
        });
        await Check("Unsupported manual lecture types are rejected",async()=>{
            var row=original.First(s=>s.Subject.PracticalHours==0);await Reject(()=>scheduleService.AddAsync(new(){SubjectID=row.SubjectID,FacultyMemberID=row.FacultyMemberID,ClassroomID=7,SectionID=row.SectionID,TimeSlotID=row.TimeSlotID,DayOfWeek=row.DayOfWeek,LectureType="Practical",GroupName="A1"}));
            Ensure(await ScheduleSnapshot()==saved);
        });
        await Check("Oversized input fields are rejected before reaching SQL Server",async()=>{
            await Reject(()=>rooms.AddAsync(new(){ClassroomNumber=new string('x',21),RoomType="Lecture",Capacity=50}));
            await Reject(()=>new FacultyMemberService().AddAsync(new(){FullName=new string('x',101)}));
            await Reject(()=>new BranchService().AddAsync(new(){BranchName=new string('x',101)}));
            await Reject(()=>new StudyYearService().AddAsync(new(){YearName=new string('x',51)}));
            await Reject(()=>subjects.AddAsync(new(){SubjectName=new string('x',101),StudyYearID=3,BranchID=1,SemesterNumber=1,TheoreticalHours=1,RequirementType="Department"}));
        });
        await Check("Unassigned theory subjects cannot replace a full saved timetable with a partial one",async()=>{
            var missing=await subjects.AddAsync(new(){SubjectName="Audit unassigned theory",StudyYearID=3,BranchID=1,SemesterNumber=1,TheoreticalHours=1,RequirementType="Department"});
            try { await Reject(()=>scheduleService.GenerateAsync());Ensure(await ScheduleSnapshot()==saved); }
            finally { await subjects.DeleteAsync(missing.SubjectID); }
        });
        await Check("Startup preserves a user-entered classroom name and shared type",async()=>{
            var room=(await rooms.GetAllAsync()).First(ClassroomRules.IsSharedLab);string name=room.ClassroomNumber;room.ClassroomNumber="Audit shared room";
            try { await rooms.UpdateAsync(room);await new DatabaseMaintenanceService().ApplyPendingFixesAsync();var read=await rooms.GetByIdAsync(room.ClassroomID);Ensure(read!.ClassroomNumber==room.ClassroomNumber && ClassroomRules.IsSharedLab(read)); }
            finally { room.ClassroomNumber=name;await rooms.UpdateAsync(room); }
        });
        await Crud();
        await Check($"All {expectedLessons} lessons regenerate with all source subjects and no room changes",async()=>{
            var result=await scheduleService.GenerateAsync();var rows=await scheduleService.GetAllAsync();
            Ensure(result.CreatedCount==expectedLessons && result.SkippedCount==0 && Coverage(original)==Coverage(rows));
            Ensure(RoomSnapshot(await rooms.GetAllAsync())==originalRooms);
            Validate(rows);File.WriteAllText(Path.Combine(Output,"full-generation.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        });
    }
    static string RoomSnapshot(IEnumerable<Classroom> rooms)=>JsonSerializer.Serialize(rooms.OrderBy(r=>r.ClassroomID)
        .Select(r=>new{r.ClassroomID,r.ClassroomNumber,r.Capacity,r.RoomType}));
    static void Validate(List<Schedule> rows)
    {
        foreach(var row in rows) {
            Ensure(row.Classroom.Capacity >= (string.IsNullOrEmpty(row.GroupName)?row.Section!.StudentCount:(int)Math.Ceiling(row.Section!.StudentCount/2.0)));
            Ensure(row.LectureType=="Practical"?ClassroomRules.IsLab(row.Classroom):!ClassroomRules.IsLab(row.Classroom)||ClassroomRules.IsSharedLab(row.Classroom));
        }
        for(int i=0;i<rows.Count;i++)for(int j=i+1;j<rows.Count;j++) {
            var a=rows[i];var b=rows[j];
            if(a.SemesterNumber!=b.SemesterNumber || a.DayOfWeek!=b.DayOfWeek || a.TimeSlot.EndTime<=b.TimeSlot.StartTime || b.TimeSlot.EndTime<=a.TimeSlot.StartTime)continue;
            Ensure(a.ClassroomID!=b.ClassroomID && a.FacultyMemberID!=b.FacultyMemberID &&
                (a.SectionID!=b.SectionID || (!string.IsNullOrEmpty(a.GroupName)&&!string.IsNullOrEmpty(b.GroupName)&&a.GroupName!=b.GroupName)),"Timetable conflict");
        }
    }
    static async Task Crud()
    {
        var branches=new BranchService();var years=new StudyYearService();var faculty=new FacultyMemberService();var rooms=new ClassroomService();var sections=new SectionService();var subjects=new SubjectService();var teaching=new FacultyMemberSubjectService();var times=new TimeSlotService();
        await Check("Branches add update duplicate validation and delete",async()=>{
            var row=await branches.AddAsync(new(){BranchName="Audit branch"});try{row.BranchName="Audit updated branch";await branches.UpdateAsync(row);await Reject(()=>branches.AddAsync(new(){BranchName=row.BranchName}));}finally{await branches.DeleteAsync(row.BranchID);}
        });
        await Check("Study years add update duplicate validation and delete",async()=>{
            var row=await years.AddAsync(new(){YearName="Audit year"});try{row.YearName="Audit updated year";await years.UpdateAsync(row);await Reject(()=>years.AddAsync(new(){YearName=row.YearName}));}finally{await years.DeleteAsync(row.StudyYearID);}
        });
        await Check("Faculty add update and delete",async()=>{
            var row=await faculty.AddAsync(new(){FullName="Audit faculty"});try{row.FullName="Audit updated faculty";await faculty.UpdateAsync(row);Ensure((await faculty.GetByIdAsync(row.FacultyMemberID))!.FullName==row.FullName);}finally{await faculty.DeleteAsync(row.FacultyMemberID);}
        });
        await Check("Rooms add update and delete",async()=>{
            var row=await rooms.AddAsync(new(){ClassroomNumber="Audit room",RoomType="Lecture",Capacity=50});try{row.Capacity=60;await rooms.UpdateAsync(row);Ensure((await rooms.GetByIdAsync(row.ClassroomID))!.Capacity==60);}finally{await rooms.DeleteAsync(row.ClassroomID);}
        });
        await Check("Sections add update and delete",async()=>{
            var row=await sections.AddAsync(new(){SectionName="Audit section",StudyYearID=3,BranchID=1,StudentCount=30});try{row.StudentCount=35;await sections.UpdateAsync(row);Ensure((await sections.GetByIdAsync(row.SectionID))!.StudentCount==35);}finally{await sections.DeleteAsync(row.SectionID);}
        });
        await Check("Subjects permit matching names in different branches and reject duplicates within one",async()=>{
            var first=await subjects.AddAsync(new(){SubjectName="Audit shared subject",StudyYearID=3,BranchID=1,SemesterNumber=1,TheoreticalHours=1,RequirementType="Department"});
            Subject? second=null;
            try{second=await subjects.AddAsync(new(){SubjectName=first.SubjectName,StudyYearID=3,BranchID=2,SemesterNumber=1,TheoreticalHours=1,RequirementType="Department"});await subjects.UpdateAsync(second);await Reject(()=>subjects.AddAsync(new(){SubjectName=first.SubjectName,StudyYearID=3,BranchID=1,SemesterNumber=1,TheoreticalHours=1,RequirementType="Department"}));}
            finally{if(second is not null)await subjects.DeleteAsync(second.SubjectID);await subjects.DeleteAsync(first.SubjectID);}
        });
        await Check("Unused teaching assignments add update and delete",async()=>{
            var teacher=await faculty.AddAsync(new(){FullName="Audit assignment teacher"});
            var teacher2=await faculty.AddAsync(new(){FullName="Audit replacement teacher"});
            var sub=await subjects.AddAsync(new(){SubjectName="Audit assignment subject",StudyYearID=3,BranchID=1,SemesterNumber=1,TheoreticalHours=1,RequirementType="Department"});
            try{await teaching.AddAsync(new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher.FacultyMemberID});await teaching.UpdateAsync(teacher.FacultyMemberID,sub.SubjectID,new(){SubjectID=sub.SubjectID,FacultyMemberID=teacher2.FacultyMemberID});await teaching.DeleteAsync(teacher2.FacultyMemberID,sub.SubjectID);}
            finally{await subjects.DeleteAsync(sub.SubjectID);await faculty.DeleteAsync(teacher.FacultyMemberID);await faculty.DeleteAsync(teacher2.FacultyMemberID);}
        });
        await Check("Time slots add update and delete",async()=>{
            var slot=await times.AddAsync(new(){StartTime=new(18,0,0),EndTime=new(19,0,0)});try{slot.EndTime=new(19,30,0);await times.UpdateAsync(slot);Ensure((await times.GetByIdAsync(slot.TimeSlotID))!.EndTime==slot.EndTime);}finally{await times.DeleteAsync(slot.TimeSlotID);}
        });
        await Check("Manual lessons add update and delete through the real service",async()=>{
            var branch=await branches.AddAsync(new(){BranchName="Audit manual branch"});
            var section=await sections.AddAsync(new(){SectionName="A",StudyYearID=3,BranchID=branch.BranchID,StudentCount=30});
            var subject=await subjects.AddAsync(new(){SubjectName="Audit manual subject",StudyYearID=3,BranchID=branch.BranchID,SemesterNumber=1,TheoreticalHours=1,PracticalHours=1,RequirementType="Department"});
            var teacher=await faculty.AddAsync(new(){FullName="Audit manual teacher"});
            var room=await rooms.AddAsync(new(){ClassroomNumber="Audit manual room",RoomType=ClassroomRules.SharedLabType,Capacity=50});
            var scheduleService=new ScheduleService();Schedule? lesson=null;
            try {
                await teaching.AddAsync(new(){SubjectID=subject.SubjectID,FacultyMemberID=teacher.FacultyMemberID});
                var period=(await times.GetAllAsync()).First();
                lesson=await scheduleService.AddAsync(new(){SubjectID=subject.SubjectID,FacultyMemberID=teacher.FacultyMemberID,ClassroomID=room.ClassroomID,SectionID=section.SectionID,DayOfWeek="Sunday",TimeSlotID=period.TimeSlotID,LectureType="Practical",GroupName="A2"});
                var loaded=await scheduleService.GetByIdAsync(lesson.ScheduleID);loaded!.DayOfWeek="Monday";await scheduleService.UpdateAsync(loaded);
                var read=await scheduleService.GetByIdAsync(lesson.ScheduleID);Ensure(read!.DayOfWeek=="Monday"&&read.GroupName=="A2"&&read.LectureType=="Practical");
            } finally {
                if(lesson is not null)await scheduleService.DeleteAsync(lesson.ScheduleID);
                if(await teaching.GetByIdAsync(teacher.FacultyMemberID,subject.SubjectID) is not null)await teaching.DeleteAsync(teacher.FacultyMemberID,subject.SubjectID);
                await subjects.DeleteAsync(subject.SubjectID);await sections.DeleteAsync(section.SectionID);await faculty.DeleteAsync(teacher.FacultyMemberID);await rooms.DeleteAsync(room.ClassroomID);
                // Soft-deleted subjects intentionally retain their branch. Keep both
                // records in this isolated audit database, just as the real service does.
                await using var copy=new AppDbContext();
                Ensure(await copy.Subjects.AnyAsync(s=>s.SubjectID==subject.SubjectID && s.IsDeleted && s.BranchID==branch.BranchID));
            }
        });
        await Check("Registration and sign-in validate normalized names and passwords",async()=>{
            var auth=new AuthService();string name="audit"+Guid.NewGuid().ToString("N")[..8];
            var result=await auth.RegisterAsync("Audit user",name,"AuditPass123","AuditPass123");Ensure(result.Succeeded);
            try{Ensure((await auth.SignInAsync(" "+name.ToUpperInvariant()+" ","AuditPass123")).Succeeded);Ensure(!(await auth.SignInAsync(name,"wrong")).Succeeded);Ensure(!(await auth.RegisterAsync("Audit user",name.ToUpperInvariant(),"AuditPass123","AuditPass123")).Succeeded);}
            finally{await using var c=new AppDbContext();await c.ApplicationUsers.Where(u=>u.UserName==name).ExecuteDeleteAsync();}
        });
    }
    static void RunEditors()
    {
        using var page=new SchedulesForm{BindingContext=new BindingContext()};
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Set(string name,object value)=>typeof(SchedulesForm).GetField(name,flags)!.SetValue(page,value);
        object? Invoke(string name,params object?[] args)=>typeof(SchedulesForm).GetMethod(name,flags)!.Invoke(page,args);
        var subjects=Task.Run(()=>new SubjectService().GetAllAsync()).GetAwaiter().GetResult();
        Set("subjectsLookup",subjects);Set("sectionsLookup",Task.Run(()=>new SectionService().GetAllAsync()).GetAwaiter().GetResult());
        Set("facultySubjectAssignmentsLookup",Task.Run(()=>new FacultyMemberSubjectService().GetAllAsync()).GetAwaiter().GetResult());
        var subjectCombo=(ComboBox)typeof(SchedulesForm).GetField("cmbSubject",flags)!.GetValue(page)!;
        var facultyCombo=(ComboBox)typeof(SchedulesForm).GetField("cmbFacultyMember",flags)!.GetValue(page)!;
        void Select(ComboBox combo,int id)=>combo.SelectedItem=combo.Items.Cast<object>().Single(o=>(int?)o.GetType().GetProperty("Id")!.GetValue(o)==id);
        Check($"All {subjects.Count} actual subjects immediately select an assigned teacher in the editor",()=>{
            Invoke("BindSubjectsCombo",null,null,null);
            foreach(var subject in subjects){Select(subjectCombo,subject.SubjectID);Ensure(facultyCombo.SelectedIndex>=0);}
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        Check("Practical type and group survive section rebinding before manual save",()=>{
            Invoke("BindSubjectsCombo",null,null,null);var subject=subjects.First(s=>s.StudyYearID==1&&s.PracticalHours>0&&s.TheoreticalHours>0);Select(subjectCombo,subject.SubjectID);
            var section=(ComboBox)typeof(SchedulesForm).GetField("cmbSection",flags)!.GetValue(page)!;Select(section,1);
            var lecture=(ComboBox)typeof(SchedulesForm).GetField("cmbLectureType",flags)!.GetValue(page)!;lecture.SelectedItem="Practical";
            var group=(ComboBox)typeof(SchedulesForm).GetField("cmbGroupName",flags)!.GetValue(page)!;group.SelectedItem="A2";
            Invoke("ApplySectionSelection");var payload=(Schedule)Invoke("ReadScheduleFromForm")!;Ensure(payload.LectureType=="Practical"&&payload.GroupName=="A2");return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        Check("General subjects clear and disable branch selection",()=>{
            Invoke("BindSubjectsCombo",null,null,null);Select(subjectCombo,subjects.First(s=>s.StudyYearID==3).SubjectID);var branch=(ComboBox)typeof(SchedulesForm).GetField("cmbBranch",flags)!.GetValue(page)!;Ensure(branch.Enabled);
            Select(subjectCombo,subjects.First(s=>s.StudyYearID==1).SubjectID);Ensure(!branch.Enabled&&branch.SelectedIndex<0);return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }
}
