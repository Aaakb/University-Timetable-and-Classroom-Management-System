using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

if(args.Length!=4 || args[0] is not("prepare" or "apply"))throw new ArgumentException("prepare|apply DATABASE REQUESTED_DAYS_JSON PLAN_JSON");
bool prepare=args[0]=="prepare";
if(prepare && args[1]=="UniversityTimetable")throw new ArgumentException("Prepare requires an isolated database.");
var connection=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString){InitialCatalog=args[1]};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
var json=new JsonSerializerOptions{WriteIndented=true,PropertyNameCaseInsensitive=true};
string Json(object o)=>JsonSerializer.Serialize(o);
object Scalars(object o)=>o.GetType().GetProperties().Where(p=>p.PropertyType.IsValueType||p.PropertyType==typeof(string)).ToDictionary(p=>p.Name,p=>p.GetValue(o));
void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS "+name);}
string Name(string s)=>string.Concat(s.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).Replace('ى','ي').Replace(" ","");
var requested=JsonSerializer.Deserialize<List<Requested>>(await File.ReadAllTextAsync(args[2]),json)!;
Check(requested.Count==50 && requested.Select(r=>Name(r.Name)).Distinct().Count()==50,"Exactly 50 distinct user-supplied faculty names");
Check(requested.All(r=>r.Days.Length==r.DayCount&&r.Days.Distinct().Count()==r.DayCount),"Every day count matches the supplied list");
var assembly=typeof(ScheduleService).Assembly;
var catalogType=assembly.GetType(typeof(ScheduleService).Namespace+".ImportCatalog")!;
async Task<object> Catalog(AppDbContext db){var task=(Task)catalogType.GetMethod("LoadAsync")!.Invoke(null,[db,CancellationToken.None])!;await task;return task.GetType().GetProperty("Result")!.GetValue(task)!;}
string Version(object c)=>(string)catalogType.GetMethod("Version")!.Invoke(c,null)!;
Dictionary<Type,List<object>> Tables(object c)=>(Dictionary<Type,List<object>>)catalogType.GetProperty("Tables")!.GetValue(c)!;
await using var db=new AppDbContext();
await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
var beforeCatalog=await Catalog(db);string beforeVersion=Version(beforeCatalog);
var before=await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync();
var faculty=await db.FacultyMembers.AsNoTracking().ToListAsync();
var revisions=await db.FacultySchedulingPreferences.AsNoTracking().Where(p=>p.SemesterNumber==1).ToDictionaryAsync(p=>p.FacultyMemberID,p=>p.Revision);
string[] days=["Sunday","Monday","Tuesday","Wednesday","Thursday"];
var expected=requested.Select(r=>new FacultyScheduleRequest(faculty.Single(f=>Name(f.FullName)==Name(r.Name)).FacultyMemberID,
    1,FacultyAvailabilityMode.Strict,r.Days,TimeLimitSeconds:55){
        UseDayPreferences=true,DayPreferences=days.Select(d=>new FacultyDaySetting(d,
            r.Days.Contains(d)?FacultyPreferenceLevel.StronglyPreferred:FacultyPreferenceLevel.Prohibited)).ToArray()
    }).ToList();
Check(expected.Select(r=>r.FacultyId).Distinct().Count()==faculty.Count,"Every faculty member matched uniquely without renaming records");
Plan plan;
if(prepare)plan=new(beforeVersion,expected,[]);
else
{
    plan=JsonSerializer.Deserialize<Plan>(await File.ReadAllTextAsync(args[3]),json)!;
    Check(plan.BeforeVersion==beforeVersion,"Production academic data, timetable and existing preferences match the verified snapshot");
    Check(plan.Requests.Count==expected.Count && plan.Requests.All(p=>expected.Any(e=>e.FacultyId==p.FacultyId&&e.SelectedDays.SequenceEqual(p.SelectedDays))),"Plan implements the exact user-supplied day lists");
}
var save=typeof(FacultyPreferenceService).GetMethod("SaveInTransactionAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
var normalized=new List<FacultyScheduleRequest>();
foreach(var request in plan.Requests)
{
    var task=(Task<FacultyPreferenceProfile>)save.Invoke(null,[db,request,revisions.GetValueOrDefault(request.FacultyId),CancellationToken.None])!;
    normalized.Add((await task).Settings);
}
Check(normalized.All(r=>r.UseDayPreferences&&r.Mode==FacultyAvailabilityMode.Strict&&
    r.DayPreferences.All(d=>r.SelectedDays.Contains(d.Day)?d.Level==FacultyPreferenceLevel.StronglyPreferred:d.Level==FacultyPreferenceLevel.Prohibited)),
    "All 50 preferences use normal editable day profiles and prohibit every unlisted day");
if(prepare)
{
    var catalog=await Catalog(db);
    var imported=before.Where(s=>s.SemesterNumber==1).Select((s,i)=>new TimetableImportRow("Faculty days",i+1,s,s.IsLocked)).ToList();
    var planner=assembly.GetType(typeof(ScheduleService).Namespace+".ImportedTimetableRepairPlanner")!;
    Console.WriteLine("Finding a complete semester-one plan for all 50 day restrictions...");
    var result=planner.GetMethod("Solve")!.Invoke(null,[catalog,imported,before.Where(s=>s.SemesterNumber!=1).ToList(),55.0,CancellationToken.None,true,null,false,false])!;
    Console.WriteLine("SEARCH "+result.GetType().GetProperty("Status")!.GetValue(result));
    var solved=(List<Schedule>?)result.GetType().GetProperty("Plan")!.GetValue(result);
    if(solved is null)throw new InvalidOperationException(JsonSerializer.Serialize(result,result.GetType(),json));
    plan=new(beforeVersion,normalized,solved);
    await File.WriteAllTextAsync(args[3],JsonSerializer.Serialize(plan,json));
}
var allowed=normalized.ToDictionary(r=>r.FacultyId,r=>r.SelectedDays.ToHashSet());
Check(plan.Schedules.Count==before.Count(s=>s.SemesterNumber==1),"Every semester-one lecture retained");
foreach(var row in plan.Schedules)
{
    var old=before.Single(s=>s.ScheduleID==row.ScheduleID);
    var copy=JsonSerializer.Deserialize<Schedule>(Json(row))!;copy.DayOfWeek=old.DayOfWeek;copy.TimeSlotID=old.TimeSlotID;copy.ClassroomID=old.ClassroomID;
    Check(Json(copy)==Json(old)&&(!old.IsLocked||Json(row)==Json(old)),"Teaching identity and locks preserved for "+row.ScheduleID);
    Check(allowed[row.FacultyMemberID].Contains(row.DayOfWeek),"Allowed faculty day for "+row.ScheduleID);
}
Check(plan.Schedules.GroupBy(s=>s.SectionID).All(g=>g.Select(s=>s.DayOfWeek).Distinct().Count()<=4),"Every section uses at most four teaching days");
var changed=plan.Schedules.Where(s=>Json(s)!=Json(before.Single(b=>b.ScheduleID==s.ScheduleID))).ToList();
await FacultyScheduleWriter.ReplacePlacementsAsync(db,changed);
var validate=typeof(ScheduleService).GetMethod("ValidateAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
var all=await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync();
foreach(var row in all)await(Task)validate.Invoke(null,[db,row,true,true])!;
Check(Json(all.Where(s=>s.SemesterNumber==2).ToList())==Json(before.Where(s=>s.SemesterNumber==2).ToList()),"All semester-two lecture fields unchanged");
var afterCatalog=await Catalog(db);string afterVersion=Version(afterCatalog);
foreach(var type in new[]{typeof(Schedule),typeof(FacultySchedulingPreference),typeof(FacultyPeriodPreference)})Tables(afterCatalog)[type]=Tables(beforeCatalog)[type];
Check(Version(afterCatalog)==beforeVersion,"Rooms, subjects, theory/practical assignments, sections and periods unchanged");
var rooms=await db.Classrooms.AsNoTracking().ToListAsync();
Check(rooms.Count==11&&rooms.Count(r=>r.RoomType=="Lecture")==4&&rooms.Count(ClassroomRules.IsLab)==7&&rooms.Count(ClassroomRules.IsSharedLab)==3,"Four lecture halls and seven labs including three shared labs preserved");
var audit=new{Database=args[1],Semester=1,FacultyProfiles=normalized.Count,TotalLectures=all.Count,
    ChangedLectures=changed.Count,BeforeVersion=beforeVersion,AfterVersion=afterVersion,
    Profiles=normalized.Select(r=>new{FacultyId=r.FacultyId,Faculty=faculty.Single(f=>f.FacultyMemberID==r.FacultyId).FullName,
        AllowedDays=r.SelectedDays,ProhibitedDays=r.UnavailableDays,
        ActualDays=plan.Schedules.Where(s=>s.FacultyMemberID==r.FacultyId).Select(s=>s.DayOfWeek).Distinct().OrderBy(d=>Array.IndexOf(days,d)).ToArray()}).ToList(),
    Changes=changed.Select(s=>new{Before=Scalars(before.Single(b=>b.ScheduleID==s.ScheduleID)),After=Scalars(s)}).ToList()};
string auditPath=args[3]+(prepare?".trial.json":".applied.json");
// Ensure the audit can serialize before committing the database transaction.
string auditJson=JsonSerializer.Serialize(audit,json);
await transaction.CommitAsync();
await File.WriteAllTextAsync(auditPath,auditJson);
var preferenceService=new FacultyPreferenceService();
foreach(var request in normalized)
{
    var loaded=await preferenceService.GetAsync(request.FacultyId,1);
    Check(loaded is not null&&loaded.Settings.UseDayPreferences&&loaded.Settings.SelectedDays.SequenceEqual(request.SelectedDays),
        "Editable screen service reloads saved preferences for "+request.FacultyId);
}
Console.WriteLine(JsonSerializer.Serialize(new{audit.Database,audit.FacultyProfiles,audit.TotalLectures,audit.ChangedLectures,audit.AfterVersion},json));

record Requested(int Number,string Name,string[] Days,int DayCount);
record Plan(string BeforeVersion,List<FacultyScheduleRequest> Requests,List<Schedule> Schedules);
