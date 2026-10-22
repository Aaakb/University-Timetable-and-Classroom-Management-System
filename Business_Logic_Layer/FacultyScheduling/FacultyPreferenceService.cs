using System.Data;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed class FacultyPreferenceService
{
    private readonly Func<AppDbContext> factory;
    public FacultyPreferenceService() : this(() => new AppDbContext()) { }
    public FacultyPreferenceService(Func<AppDbContext> factory) => this.factory = factory;

    public async Task<FacultyPreferenceProfile?> GetAsync(int facultyId, int semester, CancellationToken token = default)
    {
        await using var db = factory();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var row = await db.FacultySchedulingPreferences.AsNoTracking().SingleOrDefaultAsync(
            p => p.FacultyMemberID == facultyId && p.SemesterNumber == semester, token);
        var cells = await db.FacultyPeriodPreferences.AsNoTracking().Where(p => p.FacultyMemberID == facultyId && p.SemesterNumber == semester).ToListAsync(token);
        await tx.CommitAsync(token);
        return row is null ? null : new(FacultyPreferenceRules.FromRows([row], cells).Single().Value, row.Revision);
    }

    public async Task<FacultyPreferenceProfile> SaveAsync(FacultyScheduleRequest request, long expectedRevision, CancellationToken token = default)
    {
        await using var db = factory();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var result = await SaveInTransactionAsync(db, request, expectedRevision, token);
        await tx.CommitAsync(token);
        return result;
    }

    internal static async Task<FacultyPreferenceProfile> SaveInTransactionAsync(AppDbContext db, FacultyScheduleRequest request, long expectedRevision, CancellationToken token)
    {
        request = FacultyPreferenceRules.Normalize(request) with { MaxOtherLectureChanges = 0 };
        if (!await db.FacultyMembers.AnyAsync(f => f.FacultyMemberID == request.FacultyId, token))
            throw new ArgumentException("التدريسي غير موجود.");
        var periods = await db.TimeSlots.Where(p => !p.IsBreak).ToListAsync(token);
        FacultyPreferenceRules.ValidateMatrix(request, periods);
        if (request.PreferredPeriodIds!.Except(periods.Select(p => p.TimeSlotID)).Any()) throw new ArgumentException("إحدى الفترات المفضلة لم تعد موجودة.");
        var row = await db.FacultySchedulingPreferences.SingleOrDefaultAsync(
            p => p.FacultyMemberID == request.FacultyId && p.SemesterNumber == request.Semester, token);
        if ((row?.Revision ?? 0) != expectedRevision)
            throw new InvalidOperationException("تغيرت التفضيلات في نافذة أخرى؛ حدّث بيانات التدريسي قبل الحفظ.");
        if (row is null)
        {
            row = new() { FacultyMemberID = request.FacultyId, SemesterNumber = request.Semester };
            db.FacultySchedulingPreferences.Add(row);
        }
        row.SettingsJson = JsonSerializer.Serialize(request with { PeriodPreferences = [] });
        row.Revision = checked(row.Revision + 1); row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(token);
        await db.FacultyPeriodPreferences.Where(p => p.FacultyMemberID == request.FacultyId && p.SemesterNumber == request.Semester).ExecuteDeleteAsync(token);
        if (request.UsePreferenceMatrix)
            db.FacultyPeriodPreferences.AddRange(request.PeriodPreferences.Select(p => new FacultyPeriodPreference
            { FacultyMemberID = request.FacultyId, SemesterNumber = request.Semester, DayOfWeek = p.Day, TimeSlotID = p.TimeSlotId, Level = (int)p.Level }));
        await db.SaveChangesAsync(token);
        return new(request, row.Revision);
    }

    public async Task DeleteAsync(int facultyId, int semester, long expectedRevision, CancellationToken token = default)
    {
        await using var db = factory();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var row = await db.FacultySchedulingPreferences.SingleOrDefaultAsync(p => p.FacultyMemberID == facultyId && p.SemesterNumber == semester, token);
        if ((row?.Revision ?? 0) != expectedRevision) throw new InvalidOperationException("تغيرت التفضيلات؛ حدّث البيانات أولًا.");
        if (row is not null) { db.Remove(row); await db.SaveChangesAsync(token); }
        await tx.CommitAsync(token);
    }
}
