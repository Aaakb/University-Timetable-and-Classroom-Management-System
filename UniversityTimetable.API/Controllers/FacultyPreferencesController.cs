using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/faculty-preferences"), RequestSizeLimit(256000)]
public sealed class FacultyPreferencesController(FacultyScheduleEditorService service, MutationGate gate, IMemoryCache cache) : ControllerBase
{
    private sealed record CachedPreview(string Owner, FacultySchedulePreview Preview);
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ApiFailure(401, "يرجى تسجيل الدخول.");

    [HttpGet("lookups")]
    public async Task<object> Lookups([FromQuery] int semester = 1)
    {
        if (semester is < 1 or > 2) throw new ArgumentException("اختر الفصل الدراسي.");
        return new { data = await service.GetLookupsAsync(HttpContext.RequestAborted), configured = await service.GetConfiguredFacultyAsync(semester, HttpContext.RequestAborted) };
    }
    [HttpGet("{facultyId:int}/{semester:int}")]
    public async Task<object> Get(int facultyId, int semester) => new
    {
        profile = await service.Preferences.GetAsync(facultyId, semester, HttpContext.RequestAborted),
        current = await service.GetCurrentAsync(facultyId, semester, HttpContext.RequestAborted)
    };

    [HttpPut]
    public async Task<FacultyPreferenceProfile> Save(SaveFacultyPreferences request)
    {
        if (!request.Settings.UsePreferenceMatrix) throw new ArgumentException("يلزم استخدام مصفوفة التفضيلات ذات الدرجات الثلاث.");
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        try { return await service.Preferences.SaveAsync(request.Settings, request.ExpectedRevision, HttpContext.RequestAborted); }
        catch (InvalidOperationException ex) { throw new ApiFailure(409, ex.Message); }
    }

    [HttpPost("preview")]
    public async Task<object> Preview(FacultyScheduleRequest request)
    {
        if (!request.UsePreferenceMatrix) throw new ArgumentException("يلزم استخدام مصفوفة التفضيلات.");
        FacultySchedulePreview preview;
        try { preview = await service.PreviewAsync(request, token: HttpContext.RequestAborted); }
        catch (InvalidOperationException ex) { throw new ApiFailure(409, ex.Message); }
        string? id = null;
        if (preview.CanSave)
        {
            id = Guid.NewGuid().ToString("N");
            cache.Set("faculty-preview:" + id, new CachedPreview(Owner, preview), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), Size = 1 });
        }
        return new { id, preview };
    }

    [HttpPost("commit")]
    public async Task<object> Commit(CommitFacultyPreferences request)
    {
        if (!request.Confirmed) throw new ArgumentException("يلزم تأكيد اعتماد الجدول.");
        string key = "faculty-preview:" + request.Id;
        if (!cache.TryGetValue<CachedPreview>(key, out var cached) || cached is null || cached.Owner != Owner)
            throw new ApiFailure(409, "انتهت المعاينة؛ أنشئ مقترحًا جديدًا.");
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        try { int changed = await service.SaveAsync(cached.Preview, true, HttpContext.RequestAborted); cache.Remove(key); return new { changed }; }
        catch (InvalidOperationException ex) { throw new ApiFailure(409, ex.Message); }
    }
}
public sealed record SaveFacultyPreferences(FacultyScheduleRequest Settings, long ExpectedRevision);
public sealed record CommitFacultyPreferences(string Id, bool Confirmed);
