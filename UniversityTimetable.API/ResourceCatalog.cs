using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

namespace UniversityTimetable.API;

public interface IResource
{
    Task<List<object>> List();
    Task<object?> Get(string id);
    Task<object> Add(JsonElement body);
    Task<object> Update(string id, JsonElement body);
    Task Delete(string id);
    string Key(object dto);
}

public sealed class Resource<TEntity, TDto>(Func<Task<List<TEntity>>> list,
    Func<string, Task<TEntity?>> get, Func<TDto, Task<TEntity>> add,
    Func<string, TDto, Task<TEntity>> update, Func<string, Task> delete,
    Func<TEntity, TDto> map, Func<TDto, string> key) : IResource where TEntity : class where TDto : class
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public async Task<List<object>> List() => (await list()).Select(x => (object)map(x)).ToList();
    public async Task<object?> Get(string id) { var entity = await get(id); return entity is null ? null : map(entity); }
    private static TDto Read(JsonElement body)
    {
        var dto = body.Deserialize<TDto>(Options) ?? throw new ValidationException("Request body is required.");
        Validator.ValidateObject(dto, new ValidationContext(dto), true);
        return dto;
    }
    public async Task<object> Add(JsonElement body) => map(await add(Read(body)));
    public async Task<object> Update(string id, JsonElement body) => map(await update(id, Read(body)));
    public Task Delete(string id) => delete(id);
    public string Key(object dto) => key((TDto)dto);
}

public sealed class AssignmentDto
{
    [Range(1, int.MaxValue)] public int FacultyMemberID { get; set; }
    [Range(1, int.MaxValue)] public int SubjectID { get; set; }
    public string? SubjectName { get; set; }
    public bool IsSubjectDeleted { get; set; }
    public FacultyMemberSubject ToEntity() => new() { FacultyMemberID = FacultyMemberID, SubjectID = SubjectID };
    public static AssignmentDto From(FacultyMemberSubject x) => new() { FacultyMemberID = x.FacultyMemberID, SubjectID = x.SubjectID,
        SubjectName = x.Subject?.SubjectName, IsSubjectDeleted = x.Subject?.IsDeleted == true };
}

public sealed class ResourceCatalog : Dictionary<string, IResource>
{
    public ResourceCatalog() : base(StringComparer.OrdinalIgnoreCase)
    {
        var branches = new BranchService();
        Add("branches", new Resource<Branch, BranchDto>(
            branches.GetAllAsync, id => branches.GetByIdAsync(ParseId(id)),
            dto => branches.AddAsync(dto.ToEntity(0)),
            (id, dto) => branches.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => branches.DeleteAsync(ParseId(id)), BranchDto.From, x => x.BranchID.ToString()));
        var studyyears = new StudyYearService();
        Add("studyyears", new Resource<StudyYear, StudyYearDto>(
            studyyears.GetAllAsync, id => studyyears.GetByIdAsync(ParseId(id)),
            dto => studyyears.AddAsync(dto.ToEntity(0)),
            (id, dto) => studyyears.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => studyyears.DeleteAsync(ParseId(id)), StudyYearDto.From, x => x.StudyYearID.ToString()));
        var classrooms = new ClassroomService();
        Add("classrooms", new Resource<Classroom, ClassroomDto>(
            classrooms.GetAllAsync, id => classrooms.GetByIdAsync(ParseId(id)),
            dto => classrooms.AddAsync(dto.ToEntity(0)),
            (id, dto) => classrooms.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => classrooms.DeleteAsync(ParseId(id)), ClassroomDto.From, x => x.ClassroomID.ToString()));
        var facultymembers = new FacultyMemberService();
        Add("facultymembers", new Resource<FacultyMember, FacultyMemberDto>(
            facultymembers.GetAllAsync, id => facultymembers.GetByIdAsync(ParseId(id)),
            dto => facultymembers.AddAsync(dto.ToEntity(0)),
            (id, dto) => facultymembers.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => facultymembers.DeleteAsync(ParseId(id)), FacultyMemberDto.From, x => x.FacultyMemberID.ToString()));
        var sections = new SectionService();
        Add("sections", new Resource<Section, SectionDto>(
            sections.GetAllAsync, id => sections.GetByIdAsync(ParseId(id)),
            dto => sections.AddAsync(dto.ToEntity(0)),
            (id, dto) => sections.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => sections.DeleteAsync(ParseId(id)), SectionDto.From, x => x.SectionID.ToString()));
        var subjects = new SubjectService();
        Add("subjects", new Resource<Subject, SubjectDto>(
            subjects.GetAllAsync, id => subjects.GetByIdAsync(ParseId(id)),
            dto => subjects.AddAsync(dto.ToEntity(0)),
            (id, dto) => subjects.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => subjects.DeleteAsync(ParseId(id)), SubjectDto.From, x => x.SubjectID.ToString()));
        var timeslots = new TimeSlotService();
        Add("timeslots", new Resource<TimeSlot, TimeSlotDto>(
            timeslots.GetAllAsync, id => timeslots.GetByIdAsync(ParseId(id)),
            dto => timeslots.AddAsync(dto.ToEntity(0)),
            (id, dto) => timeslots.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => timeslots.DeleteAsync(ParseId(id)), TimeSlotDto.From, x => x.TimeSlotID.ToString()));
        var schedules = new ScheduleService();
        Add("schedules", new Resource<Schedule, ScheduleDto>(
            schedules.GetAllAsync, id => schedules.GetByIdAsync(ParseId(id)),
            dto => schedules.AddAsync(dto.ToEntity(0)),
            (id, dto) => schedules.UpdateAsync(dto.ToEntity(ParseId(id))),
            id => schedules.DeleteAsync(ParseId(id)), ScheduleDto.From, x => x.ScheduleID.ToString()));
        var assignments = new FacultyMemberSubjectService();
        Add("assignments", new Resource<FacultyMemberSubject, AssignmentDto>(assignments.GetAllAsync,
            id => { var (a,b) = AssignmentKey(id); return assignments.GetByIdAsync(a,b); },
            dto => assignments.AddAsync(dto.ToEntity()),
            (id,dto) => { var (a,b) = AssignmentKey(id); return assignments.UpdateAsync(a,b,dto.ToEntity()); },
            id => { var (a,b) = AssignmentKey(id); return assignments.DeleteAsync(a,b); },
            AssignmentDto.From, x => $"{x.FacultyMemberID}-{x.SubjectID}"));
    }
    private static int ParseId(string id) => int.TryParse(id, out int value) && value > 0
        ? value : throw new ApiFailure(400, "Invalid record identifier.");
    private static (int,int) AssignmentKey(string id)
    {
        var parts = id.Split('-');
        if (parts.Length != 2) throw new ApiFailure(400, "Invalid assignment identifier.");
        return (ParseId(parts[0]), ParseId(parts[1]));
    }
}
