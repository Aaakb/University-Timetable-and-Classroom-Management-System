using System.Data;
using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using ImportUiText = University_Timetable_and_Classroom_Management_System.ImportUiText;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/imports"), RequestSizeLimit(21 * 1024 * 1024)]
public sealed class ImportsController(IMemoryCache cache, MutationGate gate) : ControllerBase
{
    private sealed class Session(string owner, ExcelWorkbookData workbook)
    {
        public string Owner { get; } = owner;
        public ExcelWorkbookData Workbook { get; } = workbook;
        public ExcelImportPreview? Preview { get; set; }
        public ExcelImportMode Mode { get; set; }
        public string Revision { get; set; } = "";
    }
    private readonly ExcelImportService service = new();
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private Session Find(string id) => cache.TryGetValue<Session>("import:" + id, out var session) && session?.Owner == Owner
        ? session : throw new ApiFailure(409, "انتهت جلسة الاستيراد. أعد فتح الملف.");

    [HttpGet("schemas")]
    public object Schemas() => ExcelImportSchema.AcademicInputs.Select(s => new
    {
        s.Name, label = ImportLabels.Display(s.Name), s.Required,
        columns = s.Columns.Select(c => new { name = c, label = ImportLabels.Display(c) })
    });

    [HttpGet("template")]
    public async Task<IActionResult> Template([FromQuery] ExcelImportMode mode, CancellationToken token)
    {
        if (mode != ExcelImportMode.AcademicData) throw new ArgumentException("الاستيراد متاح للتدريسيين والمواد فقط.");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            await Task.Run(() => ExcelImportTemplates.Create(path, mode), token);
            return File(await System.IO.File.ReadAllBytesAsync(path, token), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", mode + "-template.xlsx");
        }
        finally { System.IO.File.Delete(path); }
    }

    [HttpPost("upload"), RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public async Task<object> Upload([FromForm] IFormFile file, CancellationToken token)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + Path.GetExtension(file.FileName).ToLowerInvariant());
        try
        {
            await TransferFiles.WriteAsync(file, path, [".xlsx"], token);
            ExcelWorkbookData workbook;
            try
            {
                if (Path.GetExtension(path) == ".xlsx")
                {
                    using var archive = ZipFile.OpenRead(path);
                    if (archive.Entries.Sum(e => e.Length) > 100L * 1024 * 1024)
                        throw new ArgumentException("حجم محتوى Excel بعد فك الضغط أكبر من الحد المسموح.");
                }
                workbook = await Task.Run(() => ImportFileReader.Read(path, token), token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { throw new ArgumentException("تعذرت قراءة الملف. تحقق من سلامته وأنه Excel بصيغة .xlsx."); }
            if (workbook.Sheets.Count > 100 || workbook.Sheets.Sum(s => (long)s.Table.Rows.Count * s.Table.Columns.Count) > 500000)
                throw new ArgumentException("الملف يحتوي بيانات كثيرة. قسّمه إلى ملفات أصغر.");
            string id = Guid.NewGuid().ToString("N");
            cache.Set("import:" + id, new Session(Owner, workbook), new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20) });
            return new { id, sheets = workbook.Sheets.Select((s, index) => new
            {
                index, s.Name, s.SourceName, s.StudySession, s.IsWeeklyTimetable, s.SourceRows,
                schema = s.SuggestedSchemaName ?? ImportLabels.MatchSheet(s.Name),
                columns = s.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName),
                rows = s.Table.Rows.Cast<DataRow>().Select(r => r.ItemArray.Select(v => Convert.ToString(v) ?? "")),
                mappings = ExcelImportSchema.AcademicInputs.ToDictionary(schema => schema.Name,
                    schema => s.Table.Columns.Cast<DataColumn>().Select(c => ImportLabels.MatchColumn(c.ColumnName, schema.Columns) ?? "").ToArray())
            }) };
        }
        finally { System.IO.File.Delete(path); }
    }

    [HttpPost("validate")]
    public async Task<object> Validate(ImportValidation request, CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        var session = Find(request.Id);
        session.Preview = null;
        if (request.Mode != ExcelImportMode.AcademicData || request.Semester is not (1 or 2)) throw new ArgumentException("خيارات الاستيراد غير صالحة.");
        if (request.Sheets is null || request.Sheets.Count == 0 || request.Sheets.Count > session.Workbook.Sheets.Count ||
            request.Sheets.Select(s => s.Index).Distinct().Count() != request.Sheets.Count) throw new ArgumentException("اختر أوراق الاستيراد دون تكرار.");
        var sheets = new List<ExcelSheetData>();
        foreach (var input in request.Sheets)
        {
            if (input.Index < 0 || input.Index >= session.Workbook.Sheets.Count) throw new ArgumentException("الورقة غير موجودة.");
            var original = session.Workbook.Sheets[input.Index];
            var schema = request.Mode == ExcelImportMode.AcademicData
                ? ExcelImportSchema.AcademicInputs.FirstOrDefault(s => s.Name == input.Schema) : ExcelImportSchema.Timetable;
            if (schema is null) throw new ArgumentException("اختر نوع البيانات لكل ورقة.");
            if (input.Mapping is null || input.Mapping.Length != original.Table.Columns.Count || input.Rows is null ||
                input.Rows.Length != original.Table.Rows.Count) throw new ArgumentException("بنية الورقة غير صالحة. أعد فتح الملف.");
            var columns = input.Mapping.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count || columns.Any(c => !schema.Columns.Contains(c)))
                throw new ArgumentException("لا يمكن ربط عمودين بالحقل نفسه. اختر حقلاً صحيحًا أو تجاهل العمود.");
            var table = new DataTable(schema.Name);
            foreach (string column in columns) table.Columns.Add(column, typeof(string));
            foreach (var row in input.Rows)
            {
                token.ThrowIfCancellationRequested();
                if (row is null || row.Length != input.Mapping.Length || row.Any(c => c is null || c.Length > 32000)) throw new ArgumentException("بيانات الخلايا غير صالحة.");
                table.Rows.Add(row.Where((_, i) => !string.IsNullOrWhiteSpace(input.Mapping[i])).Cast<object>().ToArray());
            }
            sheets.Add(new ExcelSheetData(schema.Name, table, original.SourceRows) { SourceName = original.SourceName,
                IsWeeklyTimetable = original.IsWeeklyTimetable, StudySession = original.StudySession });
        }
        if (request.SkipInvalid && request.Mode != ExcelImportMode.AcademicData) throw new ArgumentException("تخطي الصفوف متاح للبيانات الأكاديمية فقط.");
        session.Mode = request.Mode;
        session.Preview = request.SkipInvalid
            ? await service.ValidateSkippingInvalidAcademicRowsAsync(sheets, request.UpdateExisting, token)
            : await service.ValidateAsync(request.Mode, sheets, request.UpdateExisting, request.Semester, token);
        return Preview(session);
    }

    [HttpPost("save")]
    public async Task<object> Save(ImportCommit request, CancellationToken token)
    {
        if (!request.Confirmed) throw new ArgumentException("يجب تأكيد حفظ الاستيراد بعد مراجعة المعاينة.");
        using var lease = await gate.EnterAsync(token);
        var session = Find(request.Id);
        var preview = RequirePreview(session, request.Revision);
        if (!preview.IsValid) throw new ArgumentException("صحح الأخطاء وأعد التحقق قبل الحفظ.");
        try
        {
            var result = await service.SaveAsync(preview, true, token: token);
            cache.Remove("import:" + request.Id);
            return result;
        }
        catch (InvalidOperationException) { session.Preview = null; throw new ApiFailure(409, "تغيرت البيانات أو تعذر حفظ الاستيراد. أعد التحقق؛ أُلغيت تغييرات هذه العملية."); }
    }

    private static ExcelImportPreview RequirePreview(Session session, string revision) => session.Preview is not null && session.Revision == revision
        ? session.Preview : throw new ApiFailure(409, "المعاينة غير متاحة أو قديمة. أعد التحقق من البيانات.");
    private static object Preview(Session session)
    {
        session.Revision = Guid.NewGuid().ToString("N");
        var p = session.Preview!;
        // Localize copies only; changing the validated object would invalidate its integrity hash.
        IEnumerable<ImportIssue> Issues(IEnumerable<ImportIssue> issues) => issues.Select(i => i with
            { Column = ImportLabels.Display(i.Column), Message = ImportUiText.Message(i.Message) });
        return new { revision = session.Revision, preview = new
        {
            p.IsValid, p.GenerationCompleted, p.RemainingLectureCount, p.Changes, p.Lectures, p.PlacementChanges,
            issues = Issues(p.Issues), repairableConflicts = Issues(p.RepairableConflicts),
            readingNotes = Issues(p.ReadingNotes), skippedRows = Issues(p.SkippedRows),
            repairSearchStatus = ImportUiText.Message(p.RepairSearchStatus),
            unscheduledLectures = p.UnscheduledLectures.Select(ImportUiText.Message)
        } };
    }
}

public sealed record ImportSheetInput(int Index, string Schema, string[] Mapping, string[][] Rows);
public sealed record ImportValidation(string Id, ExcelImportMode Mode, int Semester, bool UpdateExisting, bool SkipInvalid, List<ImportSheetInput> Sheets);
public sealed record ImportGeneration(string Id, string Revision, double Seconds);
public sealed record ImportCommit(string Id, string Revision, bool Confirmed);
