using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/schedules")]
public sealed class SchedulingController(ScheduleService service, SchedulePdfExportService pdf,
    ResourceCatalog catalog, MutationGate gate) : ControllerBase
{
    private async Task<List<ScheduleDetailsView>> Filter(ScheduleFilter filter)
    {
        var rows = await service.GetScheduleDetailsByFiltersAsync(filter.SemesterNumber, filter.StudyYearId,
            filter.BranchId, filter.SectionId, filter.LectureType);
        return rows.Where(x => (!filter.FacultyMemberId.HasValue || x.FacultyMemberID == filter.FacultyMemberId) &&
            (!filter.ClassroomId.HasValue || x.ClassroomID == filter.ClassroomId) &&
            (string.IsNullOrEmpty(filter.DayOfWeek) || x.DayOfWeek == filter.DayOfWeek) &&
            (string.IsNullOrEmpty(filter.GroupName) || (x.LectureType == "Practical" && !string.IsNullOrEmpty(x.GroupName)
                ? string.Equals(x.GroupName, filter.GroupName, StringComparison.OrdinalIgnoreCase)
                : string.Equals(x.SectionName, AcademicStructureRules.GetBaseSectionName(filter.GroupName), StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(filter.Q) || $"{x.SubjectName} {x.FacultyMemberName} {x.ClassroomNumber} {x.SectionName} {x.YearName} {x.BranchName}"
                .Contains(filter.Q, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    [HttpGet("details")]
    public async Task<object> Details([FromQuery] ScheduleFilter filter) => new { items = await Filter(filter) };

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ScheduleFilter filter)
    {
        var rows = await Filter(filter);
        if (rows.Count == 0) throw new ApiFailure(400, "لا توجد محاضرات ضمن عوامل التصفية الحالية.");
        string path = Path.Combine(Path.GetTempPath(), $"university-timetable-{Guid.NewGuid():N}.pdf");
        try
        {
            await pdf.ExportAsync(path, rows.Select(x => new SchedulePdfRow(x.SemesterNumber, x.YearName,
                x.BranchName ?? "-", x.SectionName, x.GroupName ?? "-", x.LectureType, x.SubjectName,
                x.FacultyMemberName, x.ClassroomNumber, x.DayOfWeek,
                x.StartTime.ToString(@"hh\:mm"), x.EndTime.ToString(@"hh\:mm"))).ToList(),
                new SchedulePdfExportOptions(filter.FacultyMemberId.HasValue ? rows[0].FacultyMemberName : null));
            return File(await System.IO.File.ReadAllBytesAsync(path), "application/pdf", "University-Timetable.pdf");
        }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
    }

    private async Task<object> State()
    {
        // Include source resources as well as timetable rows, ordered by stable keys.
        var state = new SortedDictionary<string, object>();
        foreach (var (name, resource) in catalog.OrderBy(x => x.Key))
            state[name] = (await resource.List()).OrderBy(resource.Key, StringComparer.Ordinal).ToArray();
        await using var db = new AppDbContext();
        state["facultyPreferenceProfiles"] = await db.FacultySchedulingPreferences.AsNoTracking().OrderBy(p => p.FacultyMemberID).ThenBy(p => p.SemesterNumber).ToListAsync();
        state["facultyPreferencePeriods"] = await db.FacultyPeriodPreferences.AsNoTracking().OrderBy(p => p.FacultyMemberID).ThenBy(p => p.SemesterNumber).ThenBy(p => p.DayOfWeek).ThenBy(p => p.TimeSlotID).ToListAsync();
        return state;
    }

    [HttpGet("generation-state")]
    public async Task<object> GenerationState() => new { version = Versions.Of(await State()),
        warning = "سيُنشأ جدول جديد للفصلين من المواد والتكليفات الحالية، بغض النظر عن الفلاتر. ستبقى المحاضرات المثبتة في مواقعها. تُحفظ نسخة تلقائية من الجدول الحالي قبل الاستبدال، وإذا فشل التوليد يبقى الجدول الحالي كما هو. هل تريد توليد جدول جديد؟" };

    [HttpPost("generate")]
    public async Task<object> Generate(GenerateRequest request)
    {
        if (!request.ConfirmReplacement) throw new ApiFailure(400, "يجب تأكيد توليد جدول جديد واستبدال الجدول الحالي.");
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        if (request.Version != Versions.Of(await State())) throw new ApiFailure(412, "تغيرت بيانات الجدولة. راجعها ثم أكد التوليد مجددًا.");
        return await service.RegenerateAsync();
    }
}

public sealed record GenerateRequest(bool ConfirmReplacement, [Required] string Version);
public sealed class ScheduleFilter
{
    [Range(1,2)] public int? SemesterNumber { get; set; }
    public int? StudyYearId { get; set; }
    public int? BranchId { get; set; }
    public int? SectionId { get; set; }
    public int? FacultyMemberId { get; set; }
    public int? ClassroomId { get; set; }
    public string? LectureType { get; set; }
    public string? GroupName { get; set; }
    public string? DayOfWeek { get; set; }
    [StringLength(200)] public string? Q { get; set; }
}
