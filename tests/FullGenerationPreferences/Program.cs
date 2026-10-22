using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

if(args.Length!=1||!args[0].StartsWith("UniversityTimetable_GenerationPrefs_",StringComparison.Ordinal))
    throw new ArgumentException("Supply an isolated UniversityTimetable_GenerationPrefs_* copy, never the live database.");
var connection=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString){InitialCatalog=args[0]};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
Trace.Listeners.Add(new ConsoleTraceListener());
string Json(object value)=>JsonSerializer.Serialize(value);
void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
async Task<List<Schedule>> Rows(){await using var db=new AppDbContext();return await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync();}
string Counts(IEnumerable<Schedule> rows)=>Json(rows.GroupBy(s=>new{s.SubjectID,s.SectionID,s.SemesterNumber,s.LectureType,s.GroupName})
    .Select(g=>new{Key=Json(g.Key),Count=g.Count()}).OrderBy(g=>g.Key));
var catalogType=typeof(ScheduleService).Assembly.GetType(typeof(ScheduleService).Namespace+".ImportCatalog")!;
async Task<string> Resources()
{
    await using var db=new AppDbContext();var task=(Task)catalogType.GetMethod("LoadAsync")!.Invoke(null,[db,CancellationToken.None])!;
    await task;var data=task.GetType().GetProperty("Result")!.GetValue(task)!;
    var tables=(Dictionary<Type,List<object>>)catalogType.GetProperty("Tables")!.GetValue(data)!;
    tables[typeof(Schedule)]=[];
    return(string)catalogType.GetMethod("Version")!.Invoke(data,null)!;
}
var initial=await Rows();string resources=await Resources();string counts=Counts(initial);
await using(var db=new AppDbContext())
{
    Check(initial.Count==263&&await db.FacultySchedulingPreferences.CountAsync(p=>p.SemesterNumber==1)==50,"Full 263-lecture fixture includes all 50 strict day profiles");
    var rooms=await db.Classrooms.AsNoTracking().ToListAsync();
    Check(rooms.Count==11&&rooms.Count(r=>r.RoomType=="Lecture")==4&&rooms.Count(ClassroomRules.IsLab)==7&&rooms.Count(ClassroomRules.IsSharedLab)==3,"Fixture uses four halls and seven labs, three shared");
}
var service=new ScheduleService();
Directory.CreateDirectory("output/generation-preferences-fix-20261023");
for(int run=1;run<=2;run++)
{
    var locked=new List<Schedule>();
    if(run==2)
    {
        await using var db=new AppDbContext();
        foreach(int semester in new[]{1,2})
        {
            var row=await db.Schedules.Where(s=>s.SemesterNumber==semester).OrderBy(s=>s.ScheduleID).FirstAsync();
            row.IsLocked=true;
        }
        await db.SaveChangesAsync();
        locked=await db.Schedules.AsNoTracking().Where(s=>s.IsLocked).OrderBy(s=>s.ScheduleID).ToListAsync();
    }
    string backup=Path.GetFullPath($"output/generation-preferences-fix-20261023/trial-{run}-{Guid.NewGuid():N}.uttbackup.json");
    Console.WriteLine("REGENERATE "+run);
    var result=await service.RegenerateAsync(backup);
    var rows=await Rows();
    Check(result.IsRegenerated&&result.CreatedCount==263&&result.RequiredCount==263&&result.SkippedCount==0&&rows.Count==263,"Full regeneration "+run+" places all 263 required lectures");
    Check(Counts(rows)==counts,"Every subject/section/type/group weekly count preserved");
    Check(resources==await Resources(),"Rooms, capacities, assignment components, periods and all preferences unchanged");
    Check(File.Exists(result.SafetyBackupPath)&&new FileInfo(result.SafetyBackupPath!).Length>0,"Previous timetable backed up before replacement");
    Check(rows.GroupBy(s=>(s.SemesterNumber,s.SectionID)).All(g=>g.Select(s=>s.DayOfWeek).Distinct().Count()<=4),"All sections satisfy the four-day limit");
    if(run==2)Check(result.PreservedCount==2&&locked.All(b=>Json(b)==Json(rows.Single(s=>s.ScheduleID==b.ScheduleID))),"Explicit locks in both semesters retain their exact IDs and placements");
    var validate=typeof(ScheduleService).GetMethod("ValidateAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
    await using(var db=new AppDbContext())foreach(var row in rows)await(Task)validate.Invoke(null,[db,row,true,true])!;
    Check(true,"All generated lectures pass type, capacity, resource-conflict and strict-day validation");
}
var saved=await Rows();var unchanged=await service.GenerateAsync();
Check(unchanged.AddedCount==0&&Json(saved)==Json(await Rows()),"Incremental generation remains a no-op for a complete timetable");
var preferenceService=new FacultyPreferenceService();
int teacher;
await using(var db=new AppDbContext())teacher=await db.FacultyMembers.Where(f=>f.FullName=="حنان عبد الولي").Select(f=>f.FacultyMemberID).SingleAsync();
var prior=await preferenceService.GetAsync(teacher,1)??throw new Exception("Missing profile");
await preferenceService.SaveAsync(prior.Settings with{DayPreferences=new[]{"Sunday","Monday","Tuesday","Wednesday","Thursday"}
    .Select(d=>new FacultyDaySetting(d,d=="Sunday"?FacultyPreferenceLevel.StronglyPreferred:FacultyPreferenceLevel.Prohibited)).ToArray()},prior.Revision);
string impossible=Json(await Rows());bool rejected=false;
try{await service.RegenerateAsync();}catch(ArgumentException){rejected=true;}
Check(rejected&&impossible==Json(await Rows()),"Genuinely impossible one-day workload preserves every saved lecture");
var impossibleProfile=await preferenceService.GetAsync(teacher,1)??throw new Exception("Missing changed profile");
await preferenceService.SaveAsync(prior.Settings,impossibleProfile.Revision);
Console.WriteLine("SUMMARY full database regeneration and rollback checks passed");
