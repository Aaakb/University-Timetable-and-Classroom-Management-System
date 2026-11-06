using System.Data;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed class StudentService
{
    public sealed record Group(int Id, string Label)
    {
        public override string ToString() => Label;
    }

    public async Task<List<Group>> GroupsAsync(int stage)
    {
        EnsureStage(stage);
        await using var db = new AppDbContext();
        var sections = await Groups(db, stage).Include(s => s.Branch).ToListAsync();
        return sections.Select(s => new Group(s.SectionID, stage < 3 ? $"شعبة {s.SectionName}" : s.Branch!.BranchName))
            .OrderBy(g => g.Label).ToList();
    }

    public async Task<List<Student>> ListAsync(int stage)
    {
        EnsureStage(stage);
        await using var db = new AppDbContext();
        return await db.Students.AsNoTracking().Where(s => s.StudyYearID == stage)
            .OrderBy(s => s.FullName).ThenBy(s => s.StudentID).ToListAsync();
    }

    public async Task<Student> AddAsync(string fullName, int stage, int? sectionId = null)
    {
        EnsureStage(stage);
        string name = ValidateName(fullName);

        await using var db = new AppDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureStageExistsAsync(db, stage);
        if (sectionId.HasValue) await EnsureGroupAsync(db, stage, sectionId.Value);
        var student = new Student { FullName = name, StudyYearID = stage, SectionID = sectionId };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return student;
    }

    public async Task RenameAsync(int studentId, string fullName)
    {
        string name = ValidateName(fullName);
        await using var db = new AppDbContext();
        int updated = await db.Students.Where(s => s.StudentID == studentId)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.FullName, name));
        if (updated == 0) throw new ArgumentException("سجل الطالب غير موجود. حدّث القائمة وحاول مرة أخرى.");
    }

    public async Task DeleteAsync(int studentId, int stage, int? sectionId)
    {
        EnsureStage(stage);
        await using var db = new AppDbContext();
        int deleted = await db.Students.Where(s => s.StudentID == studentId && s.StudyYearID == stage && s.SectionID == sectionId)
            .ExecuteDeleteAsync();
        if (deleted == 0) throw new ArgumentException("الطالب لم يعد في القائمة الحالية. حدّث القائمة وحاول مرة أخرى.");
    }

    public sealed record ImportRow(string Name, string Location, string? Reason);
    public sealed record ImportResult(IReadOnlyList<ImportRow> Imported, IReadOnlyList<ImportRow> SkippedRows)
    {
        public int Added => Imported.Count;
        public int Skipped => SkippedRows.Count;
    }

    public async Task<(int Added, int Skipped)> ImportAsync(string path, int stage, int? sectionId = null)
    {
        var result = await ImportWithDetailsAsync(path, stage, sectionId);
        return (result.Added, result.Skipped);
    }

    public async Task<ImportResult> ImportWithDetailsAsync(string path, int stage, int? sectionId = null)
    {
        EnsureStage(stage);
        var names = await Task.Run(() => StudentNamesFile.Read(path));
        await using var db = new AppDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureStageExistsAsync(db, stage);
        if (sectionId.HasValue) await EnsureGroupAsync(db, stage, sectionId.Value);
        var existing = await db.Students.Where(s => s.StudyYearID == stage && s.SectionID == sectionId)
            .Select(s => s.FullName).ToListAsync();
        var existingNames = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = new List<ImportRow>();
        var skipped = new List<ImportRow>();
        foreach (var entry in names)
        {
            string? reason = existingNames.Contains(entry.Name)
                ? "موجود مسبقًا في المرحلة والشعبة أو التخصص المحدد"
                : !seenInFile.Add(entry.Name) ? "اسم مكرر داخل الملف" : null;
            if (reason is not null)
            {
                skipped.Add(new(entry.Name, entry.Location, reason));
                continue;
            }
            imported.Add(new(entry.Name, entry.Location, null));
        }

        db.Students.AddRange(imported.Select(row => new Student
        {
            FullName = row.Name, StudyYearID = stage, SectionID = sectionId
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(imported, skipped);
    }

    public async Task<int> PromoteAsync(int stage, IReadOnlyCollection<int> studentIds, int? destinationId = null)
    {
        EnsureStage(stage);
        if (stage == 4) throw new ArgumentException("المرحلة الرابعة هي المرحلة الأخيرة.");
        if (stage == 2 && !destinationId.HasValue) throw new ArgumentException("اختر تخصص الطلبة في المرحلة الثالثة.");
        return await MoveAsync(stage, stage + 1, studentIds, destinationId);
    }

    public Task<int> AssignGroupAsync(int stage, IReadOnlyCollection<int> studentIds, int destinationId)
    {
        EnsureStage(stage);
        return MoveAsync(stage, stage, studentIds, destinationId);
    }

    public Task<int> ChangeSectionAsync(int stage, int studentId, int sourceId, int destinationId)
    {
        EnsureSectionStage(stage);
        if (sourceId == destinationId) throw new ArgumentException("اختر شعبة أخرى للطالب.");
        return MoveAsync(stage, stage, [studentId], destinationId, sourceId);
    }

    public async Task SplitSectionAsync(int stage, int sectionId)
    {
        EnsureSectionStage(stage);
        await using var db = new AppDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureGroupAsync(db, stage, sectionId);
        var students = await db.Students.Where(s => s.StudyYearID == stage && s.SectionID == sectionId).ToArrayAsync();
        if (students.Length < 2) throw new ArgumentException("يجب أن تضم الشعبة طالبين على الأقل لتقسيمها.");

        Random.Shared.Shuffle(students.AsSpan());
        for (int index = 0; index < students.Length; index++)
            students[index].PracticalGroupNumber = index % 2 + 1;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static async Task<int> MoveAsync(int stage, int targetStage, IReadOnlyCollection<int> studentIds, int? destinationId, int? sourceId = null)
    {
        if (studentIds.Count == 0) throw new ArgumentException("ضع علامة صح أمام طالب واحد على الأقل.");
        var ids = studentIds.Distinct().ToArray();

        await using var db = new AppDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await EnsureStageExistsAsync(db, targetStage);
        if (sourceId.HasValue) await EnsureGroupAsync(db, stage, sourceId.Value);
        if (destinationId.HasValue) await EnsureGroupAsync(db, targetStage, destinationId.Value);
        var selected = await db.Students.Where(s => s.StudyYearID == stage && ids.Contains(s.StudentID) &&
            (stage != targetStage || s.SectionID == sourceId)).ToListAsync();
        if (selected.Count != ids.Length)
            throw new ArgumentException("تغيّرت قائمة الطلبة. بدّل المرحلة ثم ارجع إليها وأعد اختيار الطلبة.");

        var groupMap = new Dictionary<int, int>();
        if (!destinationId.HasValue && selected.Any(s => s.SectionID.HasValue))
        {
            var current = await Groups(db, stage).ToListAsync();
            var next = await Groups(db, targetStage).ToListAsync();
            foreach (var from in current)
            {
                var to = next.FirstOrDefault(s => stage == 1 ? s.SectionName == from.SectionName : s.BranchID == from.BranchID);
                if (to is not null) groupMap.Add(from.SectionID, to.SectionID);
            }
        }
        foreach (var student in selected)
        {
            if (destinationId.HasValue) student.SectionID = destinationId;
            else if (student.SectionID is int currentId)
            {
                if (!groupMap.TryGetValue(currentId, out int nextId))
                    throw new ArgumentException("لا توجد شعبة أو تخصص مقابل في المرحلة التالية.");
                student.SectionID = nextId;
            }
            student.StudyYearID = targetStage;
            student.PracticalGroupNumber = null;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return selected.Count;
    }

    private static IQueryable<Section> Groups(AppDbContext db, int stage) =>
        db.Sections.Where(s => s.StudyYearID == stage && (stage < 3
            ? s.BranchID == null && (s.SectionName == "A" || s.SectionName == "B")
            : s.BranchID != null && s.SectionName == "A"));

    private static async Task EnsureGroupAsync(AppDbContext db, int stage, int id)
    {
        if (!await Groups(db, stage).AnyAsync(s => s.SectionID == id))
            throw new ArgumentException("اختر شعبة أو تخصصًا من المرحلة المطلوبة.");
    }

    private static void EnsureStage(int stage)
    {
        if (stage is < 1 or > 4) throw new ArgumentException("اختر مرحلة من الأولى إلى الرابعة.");
    }

    private static void EnsureSectionStage(int stage)
    {
        if (stage is not (1 or 2)) throw new ArgumentException("هذه العملية متاحة للمرحلتين الأولى والثانية.");
    }

    private static string ValidateName(string fullName)
    {
        string name = fullName.Trim();
        if (name.Length is 0 or > 150)
            throw new ArgumentException("أدخل اسم الطالب، بحد أقصى 150 حرفًا.");
        return name;
    }

    private static async Task EnsureStageExistsAsync(AppDbContext db, int stage)
    {
        if (!await db.StudyYears.AnyAsync(y => y.StudyYearID == stage))
            throw new ArgumentException($"المرحلة {stage} غير موجودة. أضفها من تبويب المراحل أولًا.");
    }
}
