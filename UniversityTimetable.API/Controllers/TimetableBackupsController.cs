using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/timetable-backups")]
public sealed class TimetableBackupsController(IMemoryCache cache, MutationGate gate) : ControllerBase
{
    private sealed record PreviewEntry(string Owner, TimetableRestorePreview Preview);
    private sealed record DownloadEntry(string Owner, string Path);
    private readonly TimetableBackupService service = new();
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static MemoryCacheEntryOptions Expiry() => new() { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20) };

    [HttpGet("download")]
    public async Task<IActionResult> Download(CancellationToken token)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await service.SaveAsync(path, token);
            return File(await System.IO.File.ReadAllBytesAsync(path, token), "application/json", $"Timetable-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        }
        finally { System.IO.File.Delete(path); }
    }

    [HttpPost("preview"), RequestSizeLimit(21 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<object> Preview([FromForm] IFormFile file, CancellationToken token)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await TransferFiles.WriteAsync(file, path, [".json"], token);
            var preview = await service.PreviewRestoreAsync(path, token);
            string id = Guid.NewGuid().ToString("N");
            cache.Set("restore:" + id, new PreviewEntry(Owner, preview), Expiry());
            return new { id, preview };
        }
        finally { System.IO.File.Delete(path); }
    }

    [HttpPost("restore")]
    public async Task<object> Restore(TransferConfirmation request, CancellationToken token)
    {
        if (!request.Confirmed) throw new ArgumentException("يجب تأكيد استبدال الجدول الحالي بالنسخة المعروضة.");
        using var lease = await gate.EnterAsync(token);
        string key = "restore:" + request.Id;
        if (!cache.TryGetValue<PreviewEntry>(key, out var entry) || entry?.Owner != Owner)
            throw new ApiFailure(409, "انتهت معاينة النسخة. ارفع الملف وأعد المعاينة.");
        Directory.CreateDirectory(TimetableBackupService.AutomaticBackupDirectory);
        string path = Path.Combine(TimetableBackupService.AutomaticBackupDirectory, $"Before-Web-Restore-{Guid.NewGuid():N}.json");
        try
        {
            var result = await service.RestoreAsync(entry.Preview, true, path, token);
            cache.Remove(key);
            string downloadId = Guid.NewGuid().ToString("N");
            cache.Set("safety-download:" + downloadId, new DownloadEntry(Owner, path), Expiry());
            return new { result.RestoredCount, safetyDownload = $"/api/timetable-backups/safety/{downloadId}" };
        }
        catch (InvalidOperationException) { throw new ApiFailure(409, "تغيرت البيانات منذ المعاينة. أعد رفع النسخة ومعاينتها قبل الاستعادة."); }
    }

    [HttpGet("safety/{id}")]
    public async Task<IActionResult> Safety(string id, CancellationToken token)
    {
        if (!cache.TryGetValue<DownloadEntry>("safety-download:" + id, out var entry) || entry?.Owner != Owner)
            throw new ApiFailure(404, "انتهت صلاحية رابط تنزيل نسخة الأمان.");
        return File(await System.IO.File.ReadAllBytesAsync(entry.Path, token), "application/json", "Before-Restore.json");
    }
}

public sealed record TransferConfirmation(string Id, bool Confirmed);

internal static class TransferFiles
{
    public static async Task WriteAsync(IFormFile file, string path, string[] extensions, CancellationToken token)
    {
        if (file.Length is <= 0 or > 20 * 1024 * 1024) throw new ArgumentException("اختر ملفًا غير فارغ بحجم لا يتجاوز 20 MB.");
        if (!extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) throw new ArgumentException("صيغة الملف غير مدعومة.");
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(output, token);
    }
}
