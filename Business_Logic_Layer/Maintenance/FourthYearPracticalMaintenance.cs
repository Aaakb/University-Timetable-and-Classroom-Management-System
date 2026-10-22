using System.Data;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

// Explicit migration, never run silently during application startup.
public sealed class FourthYearPracticalMaintenance
{
    public async Task<FourthYearPracticalConversion> ApplyAsync(string safetyBackupPath, CancellationToken token = default)
    {
        await using var db = new AppDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var sections = await db.Sections.AsNoTracking().ToDictionaryAsync(s => s.SectionID, token);
        var subjects = await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.SubjectID, token);
        var rows = await db.Schedules.ToListAsync(token);
        var converted = new List<Schedule>();
        var duplicates = new List<Schedule>();
        foreach (var scope in rows.Where(s => s.LectureType == "Practical" && s.SectionID.HasValue &&
                     AcademicStructureRules.AllowsWholeSectionPractical(sections[s.SectionID.Value].StudyYearID))
                 .GroupBy(s => (s.SectionID, s.SubjectID, s.SemesterNumber)))
        {
            if (scope.All(s => string.IsNullOrWhiteSpace(s.GroupName))) continue;
            string name = sections[scope.Key.SectionID!.Value].SectionName.Trim().ToUpperInvariant();
            string[] priority = ["", name, name + "1", name + "2"];
            var groups = scope.GroupBy(s => (s.GroupName ?? "").Trim().ToUpperInvariant()).ToList();
            int required = subjects[scope.Key.SubjectID].EffectivePracticalLectureCount;
            if (required <= 0 || groups.Any(g => !priority.Contains(g.Key) || g.Count() != required))
                throw new InvalidOperationException($"راجع عدد محاضرات العملي للمادة {scope.Key.SubjectID} في الشعبة {scope.Key.SectionID} قبل دمج المجموعات.");
            var keep = groups.OrderBy(g => Array.IndexOf(priority, g.Key)).First().ToList();
            converted.AddRange(keep.Where(s => !string.IsNullOrWhiteSpace(s.GroupName)));
            duplicates.AddRange(scope.Except(keep));
        }
        if (duplicates.Any(s => s.IsLocked))
            throw new InvalidOperationException("توجد محاضرات مثبتة ضمن المجموعات المكررة؛ راجع الأقفال قبل الدمج.");
        if (converted.Count + duplicates.Count == 0)
            return new(0, 0, null);

        // Snapshot completes before any update or deletion. Validation failure rolls back everything.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(safetyBackupPath))!);
        await TimetableBackupService.SaveSnapshotAsync(db, safetyBackupPath);
        foreach (var row in converted) row.GroupName = null;
        db.Schedules.RemoveRange(duplicates);
        await db.SaveChangesAsync(token);
        foreach (var row in converted)
            await ScheduleService.ValidateAsync(db, row, true);
        await transaction.CommitAsync(token);
        return new(converted.Count, duplicates.Count, Path.GetFullPath(safetyBackupPath));
    }
}

public sealed record FourthYearPracticalConversion(int ConvertedLectures, int RemovedDuplicateLectures, string? SafetyBackupPath);
