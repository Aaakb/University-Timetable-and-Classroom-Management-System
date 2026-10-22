using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/timetable-alternatives"), RequestSizeLimit(256000)]
public sealed class TimetableAlternativesController(TimetableAlternativesService service, MutationGate gate, IMemoryCache cache) : ControllerBase
{
    private sealed record Cached(string Owner, TimetableAlternative Alternative);
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ApiFailure(401, "يرجى تسجيل الدخول.");
    [HttpGet]
    public Task<TimetableAlternative> Current([FromQuery] int semester = 1) => service.GetCurrentAsync(semester, HttpContext.RequestAborted);
    [HttpPost("preview")]
    public async Task<object> Preview(TimetableAlternativesRequest request)
    {
        try
        {
            var result = await service.GenerateAsync(request, HttpContext.RequestAborted);
            return new { result.Current, result.Message, result.Version, alternatives = result.Alternatives.Select(a =>
            {
                string id = Guid.NewGuid().ToString("N"); cache.Set("timetable-alternative:" + id, new Cached(Owner, a), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), Size = 1 });
                return new { id, alternative = a };
            }).ToArray() };
        }
        catch (InvalidOperationException ex) { throw new ApiFailure(409, ex.Message); }
    }
    [HttpPost("commit")]
    public async Task<object> Commit(CommitFacultyPreferences request)
    {
        if (!request.Confirmed) throw new ArgumentException("يلزم تأكيد اعتماد البديل.");
        string key = "timetable-alternative:" + request.Id;
        if (!cache.TryGetValue<Cached>(key, out var value) || value is null || value.Owner != Owner) throw new ApiFailure(409, "انتهت المعاينة؛ أنشئ البدائل مجددًا.");
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        try { int changed = await service.SaveAsync(value.Alternative, true, HttpContext.RequestAborted); cache.Remove(key); return new { changed }; }
        catch (InvalidOperationException ex) { throw new ApiFailure(409, ex.Message); }
    }
    [HttpPost("locks")]
    public async Task<object> Locks(ChangeTimetableLocks request)
    {
        if (!request.Confirmed) throw new ArgumentException("يلزم تأكيد تعديل تثبيت المحاضرات المحددة.");
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        try { return new { changed = await service.SetLocksAsync(request.Semester, request.Ids, request.Locked, request.Version, HttpContext.RequestAborted) }; }
        catch (InvalidOperationException ex) { throw new ApiFailure(409, ex.Message); }
    }
}
public sealed record ChangeTimetableLocks(int Semester, IReadOnlyList<int> Ids, bool Locked, string Version, bool Confirmed);
