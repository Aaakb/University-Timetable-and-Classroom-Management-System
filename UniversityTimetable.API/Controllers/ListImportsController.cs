using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize(Policy = WebPermissions.Administrator), Route("api/list-imports")]
[RequestSizeLimit(21 * 1024 * 1024)]
public sealed class ListImportsController(MutationGate gate) : ControllerBase
{
    [HttpPost("{kind}")]
    public async Task<object> Import(string kind, IFormFile file, CancellationToken token)
    {
        string[] extensions = kind switch
        {
            "faculty" => [".xlsx", ".csv"],
            "subjects" => [".xlsx"],
            _ => throw new ApiFailure(404, "نوع القائمة غير متاح.")
        };
        using var lease = await gate.EnterAsync(token);
        return await ReadUpload(file, extensions, token, async path =>
        {
            if (kind == "faculty")
            {
                var result = await new FacultyMemberService().ImportListAsync(path);
                return new { added = result.AddedIds.Count, skipped = result.Skipped, ids = result.AddedIds };
            }
            var subjects = await new SubjectService().ImportListAsync(path);
            return new { added = subjects.Added, skipped = subjects.Skipped };
        });
    }

    internal static async Task<object> ReadUpload(IFormFile file, string[] extensions,
        CancellationToken token, Func<string, Task<object>> read)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensions.Contains(extension)) throw new ArgumentException("صيغة الملف غير مدعومة.");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
        try
        {
            await TransferFiles.WriteAsync(file, path, extensions, token);
            if (extension == ".xlsx")
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(path);
                if (archive.Entries.Sum(entry => entry.Length) > 100 * 1024 * 1024)
                    throw new ArgumentException("حجم ملف Excel بعد فك الضغط يتجاوز الحد المسموح.");
            }
            return await read(path);
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("تعذرت قراءة الملف. تحقق من صيغته وسلامته.");
        }
        finally { System.IO.File.Delete(path); }
    }
}
