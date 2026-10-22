using System.Data;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed class TimetableBackupService
{
    private const string Format = "UniversityTimetable.ScheduleBackup";
    private const long MaximumFileBytes = 20 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 24 };
    private readonly Func<AppDbContext> factory;
    public TimetableBackupService() : this(() => new AppDbContext()) { }
    public TimetableBackupService(Func<AppDbContext> factory) => this.factory = factory;
    public static string AutomaticBackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversityTimetable", "Backups");
    internal static async Task<TimetableBackupInfo> SaveSnapshotAsync(AppDbContext db, string path)
    {
        var catalog = await ImportCatalog.LoadAsync(db, default);
        var payload = CreatePayload(catalog, db.Database.GetDbConnection().Database);
        await WriteAsync(path, payload, default, overwrite: false);
        return Info(path, payload);
    }
    public async Task<TimetableBackupInfo> SaveAsync(string path, CancellationToken token = default)
    {
        await using var db = factory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,token);
        var catalog = await ImportCatalog.LoadAsync(db,token);
        var payload = CreatePayload(catalog,db.Database.GetDbConnection().Database);
        await transaction.CommitAsync(token);
        await WriteAsync(path,payload,token);
        return Info(path,payload);
    }
    public async Task<TimetableRestorePreview> PreviewRestoreAsync(string path, CancellationToken token = default)
    {
        var document = await ReadAsync(path,token);
        await using var db = factory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,token);
        var catalog = await ImportCatalog.LoadAsync(db,token);
        var issues = await ValidateAsync(document.Payload,catalog,db,token);
        await transaction.CommitAsync(token);
        return new() { Document=document,CurrentVersion=catalog.Version(),CreatedAtUtc=document.Payload.CreatedAtUtc,
            LectureCount=document.Payload.Lectures.Count,CurrentLectureCount=catalog.Rows<Schedule>().Count,
            SemesterOneCount=document.Payload.Lectures.Count(s=>s.SemesterNumber==1),SemesterTwoCount=document.Payload.Lectures.Count(s=>s.SemesterNumber==2),Issues=issues.AsReadOnly() };
    }
    public async Task<TimetableRestoreResult> RestoreAsync(TimetableRestorePreview preview, bool confirmed,
        string safetyBackupPath, CancellationToken token = default)
    {
        if (!confirmed) throw new ArgumentException("يلزم تأكيد استعادة الجدول صراحةً.");
        if (!preview.CanRestore) throw new ArgumentException("النسخة غير جاهزة للاستعادة؛ راجع أسباب الرفض وأعد المعاينة.");
        await using var db = factory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,token);
        var catalog = await ImportCatalog.LoadAsync(db,token);
        if (catalog.Version()!=preview.CurrentVersion) throw new InvalidOperationException("تغيرت بيانات الجدول أو الموارد منذ المعاينة؛ أعد معاينة النسخة قبل الاستعادة.");
        var issues = await ValidateAsync(preview.Document!.Payload,catalog,db,token);
        if (issues.Count>0) throw new ArgumentException(string.Join(Environment.NewLine,issues.Take(8)));
        // Must finish and verify the safety file before deleting a single row.
        await WriteAsync(safetyBackupPath,CreatePayload(catalog,db.Database.GetDbConnection().Database),token,overwrite:false);
        var restored = preview.Document.Payload.Lectures.Select(s=>s.ToEntity()).ToList();
        await TimetableSnapshotStore.ReplaceAsync(db,restored,token);
        catalog.Tables[typeof(Schedule)] = restored.Cast<object>().ToList();
        if ((await ImportCatalog.LoadAsync(db,token)).Version()!=catalog.Version())
            throw new InvalidOperationException("نتيجة الاستعادة لم تطابق النسخة؛ أُلغيت العملية كاملة.");
        await transaction.CommitAsync(token);
        return new(restored.Count,Path.GetFullPath(safetyBackupPath));
    }
    private static TimetableBackupPayload CreatePayload(ImportCatalog catalog,string database)
    {
        var rows = catalog.Rows<Schedule>().OrderBy(s=>s.ScheduleID).ToList();
        return new(1,DateTimeOffset.UtcNow,database,rows.Select(TimetableBackupLecture.From).ToList(),References(catalog,rows));
    }
    private static List<TimetableBackupReference> References(ImportCatalog c,IReadOnlyList<Schedule> rows)
    {
        var refs = new List<TimetableBackupReference>();
        void Add<T>(string table,Func<T,bool> used,Func<T,string> key) where T:class
        { refs.AddRange(c.Rows<T>().Where(used).Select(x=>new TimetableBackupReference(table,key(x),ImportCatalog.ScalarJson(x)))); }
        var subjects=rows.Select(s=>s.SubjectID).ToHashSet();var faculty=rows.Select(s=>s.FacultyMemberID).ToHashSet();
        var classrooms=rows.Select(s=>s.ClassroomID).ToHashSet();var periods=rows.Select(s=>s.TimeSlotID).ToHashSet();
        var sections=rows.Select(s=>s.SectionID).ToHashSet();var years=rows.Select(s=>s.StudyYearID).ToHashSet();var branches=rows.Select(s=>s.BranchID).ToHashSet();
        var assignments=rows.Select(s=>(s.FacultyMemberID,s.SubjectID)).ToHashSet();
        Add<Subject>("Subjects",x=>subjects.Contains(x.SubjectID),x=>x.SubjectID.ToString());
        Add<FacultyMember>("FacultyMembers",x=>faculty.Contains(x.FacultyMemberID),x=>x.FacultyMemberID.ToString());
        Add<Classroom>("Classrooms",x=>classrooms.Contains(x.ClassroomID),x=>x.ClassroomID.ToString());
        Add<TimeSlot>("TimeSlots",x=>periods.Contains(x.TimeSlotID),x=>x.TimeSlotID.ToString());
        Add<Section>("Sections",x=>sections.Contains(x.SectionID),x=>x.SectionID.ToString());
        Add<StudyYear>("StudyYears",x=>years.Contains(x.StudyYearID),x=>x.StudyYearID.ToString());
        Add<Branch>("Branches",x=>branches.Contains(x.BranchID),x=>x.BranchID.ToString());
        Add<FacultyMemberSubject>("FacultyMemberSubjects",x=>assignments.Contains((x.FacultyMemberID,x.SubjectID)),x=>$"{x.FacultyMemberID}/{x.SubjectID}");
        return refs.OrderBy(r=>r.Table,StringComparer.Ordinal).ThenBy(r=>r.Key,StringComparer.Ordinal).ToList();
    }
    private static async Task<List<string>> ValidateAsync(TimetableBackupPayload payload,ImportCatalog catalog,AppDbContext db,CancellationToken token)
    {
        var issues = new List<string>();
        var rows=payload.Lectures.Select(s=>s.ToEntity()).ToList();
        var current=References(catalog,rows).ToDictionary(r=>(r.Table,r.Key));
        foreach(var reference in payload.References)
        {
            if(!current.TryGetValue((reference.Table,reference.Key),out var actual)) issues.Add($"مرجع مفقود: {reference.Table} #{reference.Key}.");
            else if(!ReferenceMatches(reference,actual)) issues.Add($"تغيرت بيانات المرجع: {reference.Table} #{reference.Key}. الاستعادة لا تغير الموارد الأكاديمية.");
        }
        var savedKeys=payload.References.Select(r=>(r.Table,r.Key)).ToHashSet();
        foreach(var missing in current.Keys.Where(k=>!savedKeys.Contains(k)))
            issues.Add($"النسخة لا تتضمن هوية المرجع: {missing.Table} #{missing.Key}.");
        if(issues.Count>0)return issues;
        foreach(var row in rows)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                string before=ImportCatalog.ScalarJson(row);
                await ScheduleService.ValidateBackupLectureAsync(db,row);
                if(before!=ImportCatalog.ScalarJson(row))issues.Add($"المحاضرة #{row.ScheduleID} تحتاج تصحيحًا؛ لا يمكن استعادتها مطابقةً للنسخة.");
            }
            catch(ArgumentException e){issues.Add($"المحاضرة #{row.ScheduleID}: {e.Message}");}
        }
        if(ScheduleConflictChecker.HasAnyPairConflict(rows))issues.Add("تحتوي النسخة على تعارضات بين محاضراتها؛ لن تُحفظ.");
        return issues;
    }
    private static string Checksum(TimetableBackupPayload payload)=>ImportCatalog.Hash(JsonSerializer.Serialize(payload,Json));
    private static bool ReferenceMatches(TimetableBackupReference saved, TimetableBackupReference current)
    {
        if (saved.Values == current.Values) return true;
        if (saved.Table != "FacultyMemberSubjects") return false;
        using var before = JsonDocument.Parse(saved.Values);
        using var after = JsonDocument.Parse(current.Values);
        // Only accept omitted legacy fields when their current values still have
        // the legacy meaning; never restore across a changed teaching component.
        var properties = after.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        if (!before.RootElement.TryGetProperty("SectionID", out _) &&
            properties["SectionID"].ValueKind == JsonValueKind.Null) properties.Remove("SectionID");
        if (!before.RootElement.TryGetProperty("LectureType", out _) &&
            properties["LectureType"].GetString() == "Both") properties.Remove("LectureType");
        return JsonSerializer.Serialize(properties) == saved.Values;
    }
    private static TimetableBackupInfo Info(string path,TimetableBackupPayload p)=>new(Path.GetFullPath(path),p.CreatedAtUtc,p.Lectures.Count,p.Lectures.Count(s=>s.SemesterNumber==1),p.Lectures.Count(s=>s.SemesterNumber==2));
    private static async Task<TimetableBackupDocument> ReadAsync(string path,CancellationToken token)
    {
        if(new FileInfo(path).Length>MaximumFileBytes)throw new ArgumentException("ملف النسخة أكبر من الحد المسموح (20 MB).");
        TimetableBackupDocument? document;
        try { await using var stream=File.OpenRead(path);document=await JsonSerializer.DeserializeAsync<TimetableBackupDocument>(stream,Json,token); }
        catch(JsonException e){throw new ArgumentException("ملف النسخة غير صالح أو غير مكتمل.",e);}
        if(document?.Format!=Format||document.Payload is null||document.Payload.Version!=1||document.Payload.Lectures is null||document.Payload.References is null)
            throw new ArgumentException("هذه ليست نسخة جدول مدعومة من التطبيق.");
        if(document.Checksum!=Checksum(document.Payload))throw new ArgumentException("فشل فحص سلامة النسخة؛ ربما عُدّل الملف أو لم يُحفظ كاملًا.");
        if(document.Payload.Lectures.Count>100000||document.Payload.Lectures.Any(s=>s is null||s.ScheduleID<=0)||document.Payload.Lectures.Select(s=>s.ScheduleID).Distinct().Count()!=document.Payload.Lectures.Count)
            throw new ArgumentException("تحتوي النسخة على معرّفات محاضرات غير صالحة أو مكررة.");
        if(document.Payload.References.Any(r=>r is null)||document.Payload.References.Select(r=>(r.Table,r.Key)).Distinct().Count()!=document.Payload.References.Count)
            throw new ArgumentException("مراجع النسخة غير صالحة أو مكررة.");
        return document;
    }
    private static async Task WriteAsync(string path,TimetableBackupPayload payload,CancellationToken token,bool overwrite=true)
    {
        string fullPath=Path.GetFullPath(path);
        string tempPath=Path.Combine(Path.GetDirectoryName(fullPath)!,"."+Path.GetFileName(fullPath)+"."+Guid.NewGuid().ToString("N")+".tmp");
        var document=new TimetableBackupDocument(Format,payload,Checksum(payload));
        try
        {
            await using(var file=new FileStream(tempPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous|FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(file,document,Json,token);await file.FlushAsync(token);file.Flush(flushToDisk:true);
            }
            await ReadAsync(tempPath,token);
            token.ThrowIfCancellationRequested();
            File.Move(tempPath,fullPath,overwrite);
        }
        finally { if(File.Exists(tempPath))File.Delete(tempPath); }
    }
}
