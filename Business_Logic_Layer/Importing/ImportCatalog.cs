using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal sealed class ImportCatalog
{
    public Dictionary<Type, List<object>> Tables { get; } = [];
    public List<T> Rows<T>() where T : class => Tables[typeof(T)].Cast<T>().ToList();
    public static async Task<ImportCatalog> LoadAsync(AppDbContext db, CancellationToken token)
    {
        var catalog = new ImportCatalog();
        await Load<StudyYear>(); await Load<Branch>(); await Load<FacultyMember>();
        await Load<Classroom>(); await Load<TimeSlot>(); await Load<Section>();
        await Load<Subject>(); await Load<FacultyMemberSubject>(); await Load<Schedule>();
        await Load<FacultySchedulingPreference>();
        await Load<FacultyPeriodPreference>();
        return catalog;
        async Task Load<T>() where T : class => catalog.Tables[typeof(T)] =
            (await db.Set<T>().AsNoTracking().ToListAsync(token)).Cast<object>().ToList();
    }
    public string Version() => Hash(string.Join("\n", Tables.OrderBy(t => t.Key.Name)
        .SelectMany(t => t.Value.Select(ScalarJson).OrderBy(x => x, StringComparer.Ordinal))));
    public static string ScalarJson(object entity) => JsonSerializer.Serialize(entity.GetType().GetProperties()
        .Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string))
        .OrderBy(p => p.Name).ToDictionary(p => p.Name, p => p.GetValue(entity)));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool Same(object a, object b) => ScalarJson(a) == ScalarJson(b);
    public static string LectureKey(Schedule s) => $"{s.SubjectID}/{s.SectionID}/{s.SemesterNumber}/{s.LectureType}/{s.GroupName?.ToUpperInvariant()}/{s.DayOfWeek}/{s.TimeSlotID}/{s.FacultyMemberID}/{s.ClassroomID}";
    public void Put(object entity, object? old)
    {
        var list = Tables[entity.GetType()];
        if (old is not null) list.Remove(old);
        list.Add(entity);
    }
}
