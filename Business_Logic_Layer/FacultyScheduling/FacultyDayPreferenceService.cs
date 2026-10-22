using System.Data;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed partial class FacultyScheduleEditorService
{
    // Saving a day profile and updating the existing semester timetable is atomic.
    // Faculty without profiles stay available on all days; their lectures may move
    // when needed, with total placement changes used as the final tie breaker.
    public async Task<FacultyDayPreferenceResult> SaveDayPreferencesAndOptimizeAsync(
        FacultyScheduleRequest request, long expectedRevision, CancellationToken token = default)
    {
        if (!request.UseDayPreferences) throw new ArgumentException("استخدم تفضيلات الأيام فقط.");
        request = FacultyPreferenceRules.Normalize(request);
        await using var db = factory();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var profile = await FacultyPreferenceService.SaveInTransactionAsync(db, request, expectedRevision, token);
        var data = await ImportCatalog.LoadAsync(db, token);
        var rows = data.Rows<Schedule>().Where(s => s.SemesterNumber == request.Semester).ToList();
        if (rows.Count == 0)
        {
            await transaction.CommitAsync(token);
            return new(profile, 0, "NoTimetable", []);
        }
        var result = await Task.Run(() => ImportedTimetableRepairPlanner.Solve(data,
            rows.Select(s => new TimetableImportRow("Existing", 0, s, s.IsLocked)).ToList(),
            data.Rows<Schedule>().Where(s => s.SemesterNumber != request.Semester).ToList(),
            request.TimeLimitSeconds, token, optimizePreferences: true), token);
        if (result.Plan is null)
            throw new InvalidOperationException(result.Status == "Infeasible"
                ? "لا يمكن تطبيق الأيام المطلوبة ضمن أربعة أيام لكل شعبة ومع القيود الحالية والمحاضرات المثبتة. لم تُحفظ التفضيلات أو تغييرات الجدول؛ راجع الأيام غير المتاحة."
                : "لم يُعثر على جدول كامل خلال مهلة البحث؛ هذا لا يثبت استحالة الحل. لم تُحفظ التفضيلات أو تغييرات الجدول؛ حاول زيادة مهلة البحث.");
        await ValidatePlanAsync(data, request, result.Plan, token, wholeSemester: true, validationDb: db);
        var originals = rows.ToDictionary(s => s.ScheduleID);
        var changed = result.Plan.Where(s => !ImportCatalog.Same(s, originals[s.ScheduleID])).ToList();
        var changes = changed.Select(row =>
        {
            var before = View(data, originals[row.ScheduleID]); var after = View(data, row);
            return new FacultyLectureChange(row.ScheduleID, before.Subject, before.Section, before.LectureType, before.GroupName,
                before.Day, before.Period, before.Room, after.Day, after.Period, after.Room, true, false)
            {
                Faculty = data.Rows<FacultyMember>().Single(f => f.FacultyMemberID == row.FacultyMemberID).FullName,
                IsOtherFaculty = row.FacultyMemberID != request.FacultyId
            };
        }).ToArray();
        foreach (var row in changed) data.Put(row, originals[row.ScheduleID]);
        await FacultyScheduleWriter.ReplacePlacementsAsync(db, changed, token);
        if ((await ImportCatalog.LoadAsync(db, token)).Version() != data.Version())
            throw new InvalidOperationException("لم تتطابق نتيجة الحفظ مع الجدول المحسّن؛ أُلغيت العملية.");
        await transaction.CommitAsync(token);
        return new(profile, changed.Count, result.Status, changes);
    }
}
