using System.Linq.Expressions;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;
namespace UniversityTimetable.API;

public static class ResourceQueries
{
    public sealed record Page(List<object> Items, int Total);
    public static async Task<Page> Read(string resource, string? search, int page, int size, CancellationToken token)
    {
        await using var db = new AppDbContext();
        return resource.ToLowerInvariant() switch
        {
            "branches" => await Read(db.Branches.OrderBy(x=>x.BranchID), BranchDto.From, search, page, size, token),
            "studyyears" => await Read(db.StudyYears.OrderBy(x=>x.StudyYearID), StudyYearDto.From, search, page, size, token),
            "classrooms" => await Read(db.Classrooms.OrderBy(x=>x.RoomType == "Lecture" ? 0 : 1).ThenBy(x=>x.ClassroomNumber).ThenBy(x=>x.ClassroomID), ClassroomDto.From, search, page, size, token),
            "facultymembers" => await Read(db.FacultyMembers.OrderBy(x=>x.FacultyMemberID), FacultyMemberDto.From, search, page, size, token),
            "timeslots" => await Read(db.TimeSlots.Where(x=>!x.IsBreak).OrderBy(x=>x.StartTime).ThenBy(x=>x.TimeSlotID), TimeSlotDto.From, search, page, size, token),
            "sections" => await Read(db.Sections.OrderBy(x=>x.SectionID), SectionDto.From, search, page, size, token),
            "subjects" => await Read(db.Subjects.Where(x=>!x.IsDeleted).OrderBy(x=>x.StudyYearID).ThenBy(x=>x.BranchID).ThenBy(x=>x.SemesterNumber).ThenBy(x=>x.SubjectID), SubjectDto.From, search, page, size, token),
            "schedules" => await Read(db.Schedules.OrderBy(x=>x.ScheduleID), ScheduleDto.From, search, page, size, token),
            "assignments" => await Read(db.FacultyMemberSubjects.Include(x=>x.Subject).OrderBy(x=>x.FacultyMemberID).ThenBy(x=>x.SubjectID), AssignmentDto.From, search, page, size, token),
            _ => throw new ApiFailure(404, "Unknown resource.")
        };
    }
    private static async Task<Page> Read<T,D>(IQueryable<T> source, Func<T,D> map, string? search, int page, int size, CancellationToken token) where T:class where D:class
    {
        source=source.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var parameter=Expression.Parameter(typeof(T),"row");
            Expression predicate=Expression.Constant(false);
            foreach(var property in typeof(T).GetProperties().Where(p=>p.PropertyType==typeof(string)))
            {
                var member=Expression.Property(parameter,property);
                var contains=Expression.Call(member,nameof(string.Contains),Type.EmptyTypes,Expression.Constant(search.Trim()));
                predicate=Expression.OrElse(predicate,Expression.AndAlso(Expression.NotEqual(member,Expression.Constant(null,typeof(string))),contains));
            }
            source=source.Where(Expression.Lambda<Func<T,bool>>(predicate,parameter));
        }
        int total=await source.CountAsync(token);
        int skip=(int)Math.Min((long)(page-1)*size,int.MaxValue);
        var rows=await source.Skip(skip).Take(size).ToListAsync(token);
        return new Page(rows.Select(x=>(object)map(x)).ToList(),total);
    }
}
