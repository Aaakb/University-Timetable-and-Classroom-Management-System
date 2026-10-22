using System.Data;
using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

if(args.Length!=4 || args[0] is not ("prepare" or "apply"))
    throw new ArgumentException("prepare|apply DATABASE DOCUMENTED_CONFIGURATION_JSON PLAN_JSON");
bool prepare=args[0]=="prepare";
if(prepare && args[1]=="UniversityTimetable") throw new ArgumentException("Prepare requires an isolated copy.");
var connection=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString){InitialCatalog=args[1]};
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,connection.ConnectionString);
var json=new JsonSerializerOptions{WriteIndented=true,PropertyNameCaseInsensitive=true};
string Json(object value)=>JsonSerializer.Serialize(value);
object Scalars(object value)=>value.GetType().GetProperties().Where(p=>p.PropertyType.IsValueType||p.PropertyType==typeof(string))
    .ToDictionary(p=>p.Name,p=>p.GetValue(value));
void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Console.WriteLine("PASS "+message);}
var catalogType=typeof(ScheduleService).Assembly.GetType(typeof(ScheduleService).Namespace+".ImportCatalog")!;
async Task<object> Catalog(AppDbContext db){var task=(Task)catalogType.GetMethod("LoadAsync")!.Invoke(null,[db,CancellationToken.None])!;await task;return task.GetType().GetProperty("Result")!.GetValue(task)!;}
string Version(object c)=>(string)catalogType.GetMethod("Version")!.Invoke(c,null)!;
Dictionary<Type,List<object>> Tables(object c)=>(Dictionary<Type,List<object>>)catalogType.GetProperty("Tables")!.GetValue(c)!;
await using var db=new AppDbContext();
await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
var catalog=await Catalog(db);string beforeVersion=Version(catalog);
var beforeRooms=await db.Classrooms.AsNoTracking().OrderBy(r=>r.ClassroomID).ToListAsync();
var beforeRows=await db.Schedules.AsNoTracking().OrderBy(s=>s.ScheduleID).ToListAsync();
Plan plan;
if(prepare)
{
    using var config=JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));
    var target=config.RootElement.GetProperty("rooms").EnumerateArray().Select(r=>new Classroom{
        ClassroomID=r.GetProperty("id").GetInt32(),ClassroomNumber=r.GetProperty("name").GetString()!,
        RoomType=r.GetProperty("type").GetString()!,
        // Restore identities and types without changing user-configured capacities.
        Capacity=beforeRooms.Single(old=>old.ClassroomID==r.GetProperty("id").GetInt32()).Capacity
    }).OrderBy(r=>r.ClassroomID).ToList();
    Check(target.Count==11 && target.Count(r=>r.RoomType=="Lecture")==4 &&
        target.Count(ClassroomRules.IsLab)==7 && target.Count(ClassroomRules.IsSharedLab)==3,
        "Documented target is four lecture halls plus seven labs, including three shared labs");
    var sections=await db.Sections.AsNoTracking().ToDictionaryAsync(s=>s.SectionID);
    var planned=JsonSerializer.Deserialize<List<Schedule>>(Json(beforeRows))!;
    bool Allowed(Classroom room,Schedule row)
    {
        var section=sections[row.SectionID!.Value];
        int capacity=string.IsNullOrWhiteSpace(row.GroupName)||AcademicStructureRules.AllowsWholeSectionPractical(section.StudyYearID)
            ? section.StudentCount:(section.StudentCount+1)/2;
        return room.Capacity>=capacity &&
            (row.LectureType=="Practical"?ClassroomRules.IsLab(room):room.RoomType=="Lecture"||ClassroomRules.IsSharedLab(room)) &&
            (!row.IsLocked||room.ClassroomID==row.ClassroomID);
    }
    var repairSemesters=new HashSet<int>();
    foreach(var slot in planned.GroupBy(s=>(s.SemesterNumber,s.DayOfWeek,s.TimeSlotID)))
    {
        if(repairSemesters.Contains(slot.Key.SemesterNumber))continue;
        var batch=slot.OrderBy(s=>target.Count(r=>Allowed(r,s))).ThenBy(s=>s.ScheduleID).ToList();
        var memo=new Dictionary<(int,int),(int Cost,int[] Rooms)>();
        (int Cost,int[] Rooms) Solve(int index,int used)
        {
            if(index==batch.Count)return(0,[]);
            if(memo.TryGetValue((index,used),out var cached))return cached;
            (int Cost,int[] Rooms) best=(1000000,[]);
            for(int i=0;i<target.Count;i++)
            {
                if((used&(1<<i))!=0||!Allowed(target[i],batch[index]))continue;
                var tail=Solve(index+1,used|(1<<i));
                int cost=tail.Cost+(target[i].ClassroomID==batch[index].ClassroomID?0:1);
                if(cost<best.Cost)best=(cost,new[]{target[i].ClassroomID}.Concat(tail.Rooms).ToArray());
            }
            return memo[(index,used)]=best;
        }
        var solution=Solve(0,0);
        if(solution.Cost>=1000000)
        {
            repairSemesters.Add(slot.Key.SemesterNumber);
            Console.WriteLine("A time adjustment is required in semester "+slot.Key.SemesterNumber);
            continue;
        }
        for(int i=0;i<batch.Count;i++)batch[i].ClassroomID=solution.Rooms[i];
    }
    Tables(catalog)[typeof(Classroom)]=target.Cast<object>().ToList();
    foreach(int semester in repairSemesters)
    {
        var originals=JsonSerializer.Deserialize<List<Schedule>>(Json(beforeRows.Where(s=>s.SemesterNumber==semester).ToList()))!;
        var imported=originals.Select((s,i)=>new TimetableImportRow("Restore rooms",i+1,s,s.IsLocked)).ToList();
        var planner=typeof(ScheduleService).Assembly.GetType(typeof(ScheduleService).Namespace+".ImportedTimetableRepairPlanner")!;
        var result=planner.GetMethod("Solve")!.Invoke(null,[catalog,imported,planned.Where(s=>s.SemesterNumber!=semester).ToList(),55.0,CancellationToken.None,false,null,false,false])!;
        Console.WriteLine("Semester "+semester+" search: "+result.GetType().GetProperty("Status")!.GetValue(result));
        var solved=(List<Schedule>?)result.GetType().GetProperty("Plan")!.GetValue(result);
        if(solved is null)throw new InvalidOperationException(JsonSerializer.Serialize(result,result.GetType(),json));
        planned.RemoveAll(s=>s.SemesterNumber==semester);planned.AddRange(solved);
    }
    // Restore the untouched catalog used for the all-other-tables audit below.
    Tables(catalog)[typeof(Classroom)]=beforeRooms.Cast<object>().ToList();
    plan=new(beforeVersion,target,planned);
    await File.WriteAllTextAsync(args[3],JsonSerializer.Serialize(plan,json));
}
else
{
    plan=JsonSerializer.Deserialize<Plan>(await File.ReadAllTextAsync(args[3]),json)!;
    Check(plan.BeforeVersion==beforeVersion,"Production snapshot exactly matches the verified trial");
}
Check(plan.Rooms.Count==11&&plan.Rooms.Count(r=>r.RoomType=="Lecture")==4&&
    plan.Rooms.Count(ClassroomRules.IsLab)==7&&plan.Rooms.Count(ClassroomRules.IsSharedLab)==3,"Exact 4+7 configuration; shared labs counted within seven");
Check(plan.Schedules.Count==beforeRows.Count,"No lecture is added or removed");
foreach(var row in plan.Schedules)
{
    var old=beforeRows.Single(s=>s.ScheduleID==row.ScheduleID);
    var copy=JsonSerializer.Deserialize<Schedule>(Json(row))!;copy.ClassroomID=old.ClassroomID;
    copy.DayOfWeek=old.DayOfWeek;copy.TimeSlotID=old.TimeSlotID;
    Check(Json(copy)==Json(old),"Teacher, type, section, group and lock preserved for "+row.ScheduleID);
    Check(!old.IsLocked||Json(row)==Json(old),"Locked placement preserved for "+row.ScheduleID);
}
var existing=await db.Classrooms.ToListAsync();
foreach(var room in plan.Rooms)db.Entry(existing.Single(r=>r.ClassroomID==room.ClassroomID)).CurrentValues.SetValues(room);
await db.SaveChangesAsync();
var changed=plan.Schedules.Where(row=>Json(row)!=Json(beforeRows.Single(s=>s.ScheduleID==row.ScheduleID))).ToList();
await FacultyScheduleWriter.ReplacePlacementsAsync(db,changed);
var removed=existing.Where(r=>!plan.Rooms.Any(p=>p.ClassroomID==r.ClassroomID)).ToList();
foreach(var room in removed)Check(!await db.Schedules.AnyAsync(s=>s.ClassroomID==room.ClassroomID),"All lectures transferred from extra room "+room.ClassroomID);
db.Classrooms.RemoveRange(removed);await db.SaveChangesAsync();
var validate=typeof(ScheduleService).GetMethod("ValidateAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
foreach(var row in plan.Schedules)await(Task)validate.Invoke(null,[db,row,true,true])!;
var after=await Catalog(db);string afterVersion=Version(after);
Tables(after)[typeof(Classroom)]=Tables(catalog)[typeof(Classroom)];
Tables(after)[typeof(Schedule)]=Tables(catalog)[typeof(Schedule)];
Check(Version(after)==beforeVersion,"All subjects, teachers, assignment components, sections, periods and preferences unchanged");
await transaction.CommitAsync();
var audit=new{Database=args[1],TotalLectures=plan.Schedules.Count,
    ChangedRooms=plan.Schedules.Count(row=>row.ClassroomID!=beforeRows.Single(s=>s.ScheduleID==row.ScheduleID).ClassroomID),
    ChangedTimes=plan.Schedules.Count(row=>{var b=beforeRows.Single(s=>s.ScheduleID==row.ScheduleID);return row.DayOfWeek!=b.DayOfWeek||row.TimeSlotID!=b.TimeSlotID;}),
    Rooms=plan.Rooms.Select(Scalars).ToList(),RemovedExtraRooms=removed.Select(Scalars).ToList(),BeforeVersion=beforeVersion,AfterVersion=afterVersion,
    Changes=changed.Select(row=>new{Before=Scalars(beforeRows.Single(s=>s.ScheduleID==row.ScheduleID)),After=Scalars(row)}).ToList()};
await File.WriteAllTextAsync(args[3]+(prepare?".trial.json":".applied.json"),JsonSerializer.Serialize(audit,json));
Console.WriteLine(JsonSerializer.Serialize(new{audit.Database,audit.TotalLectures,audit.ChangedRooms,audit.ChangedTimes,audit.AfterVersion},json));

record Plan(string BeforeVersion,List<Classroom> Rooms,List<Schedule> Schedules);
