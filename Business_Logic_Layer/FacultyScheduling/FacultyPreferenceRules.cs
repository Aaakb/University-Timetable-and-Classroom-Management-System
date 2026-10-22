using System.Text.Json;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class FacultyPreferenceRules
{
    public static FacultyScheduleRequest Normalize(FacultyScheduleRequest request)
    {
        if (request.FacultyId <= 0 || request.Semester is < 1 or > 2 || !Enum.IsDefined(request.Mode) || !Enum.IsDefined(request.Goal))
            throw new ArgumentException("اختر التدريسي والفصل ونمط الجدولة.");
        if (!double.IsFinite(request.TimeLimitSeconds) || request.TimeLimitSeconds is < .01 or > 120)
            throw new ArgumentException("مهلة البحث يجب أن تكون بين 0.01 و120 ثانية.");
        if (request.UseDayPreferences)
        {
            var daySettings = (request.DayPreferences ?? []).Select(p => p with { Day = ScheduleDayRules.NormalizeDayName(p.Day) }).ToArray();
            if (daySettings.Length != ScheduleDayRules.AllDays.Count || daySettings.Select(p => p.Day).Distinct().Count() != daySettings.Length ||
                daySettings.Any(p => !ScheduleDayRules.AllDays.Contains(p.Day) || !Enum.IsDefined(p.Level)))
                throw new ArgumentException("حدد تفضيلًا واحدًا لكل يوم من أيام الأسبوع.");
            var allowed = daySettings.Where(p => p.Level != FacultyPreferenceLevel.Prohibited).Select(p => p.Day).ToArray();
            if (allowed.Length == 0) throw new ArgumentException("يجب إتاحة يوم واحد على الأقل للتدريس.");
            // Day profiles never inherit period restrictions or exact attendance counts.
            request = request with { DayPreferences = daySettings, SelectedDays = allowed,
                UnavailableDays = daySettings.Where(p => p.Level == FacultyPreferenceLevel.Prohibited).Select(p => p.Day).ToArray(),
                Mode = FacultyAvailabilityMode.Strict, UsePreferenceMatrix = false, PeriodPreferences = [], PreferredPeriodIds = [],
                EarliestStartMinute = null, LatestEndMinute = null, TargetTeachingDays = null, MaxTeachingDays = null,
                MaxDailyMinutes = null, MaxConsecutiveMinutes = null };
        }
        var selected = (request.SelectedDays ?? []).Select(ScheduleDayRules.NormalizeDayName).Distinct().ToArray();
        var unavailable = (request.UnavailableDays ?? []).Select(ScheduleDayRules.NormalizeDayName).Distinct().ToArray();
        if (selected.Length == 0 || selected.Intersect(unavailable).Any())
            throw new ArgumentException("حدد يومًا واحدًا على الأقل؛ لا يمكن أن يكون اليوم مفضلًا وغير متاح معًا.");
        if (request.EarliestStartMinute is < 0 or >= 1440 || request.LatestEndMinute is <= 0 or >= 1440 ||
            request.EarliestStartMinute.GetValueOrDefault() >= (request.LatestEndMinute ?? 1440))
            throw new ArgumentException("وقت نهاية الإتاحة يجب أن يكون بعد بدايتها وفي اليوم نفسه.");
        if (request.TargetTeachingDays is < 1 or > 5 || request.MaxTeachingDays is < 1 or > 5 ||
            request.TargetTeachingDays > request.MaxTeachingDays || request.MaxDailyMinutes is < 1 or > 1440 ||
            request.MaxConsecutiveMinutes is < 1 or > 1440 || request.MinimumRestMinutes is < 1 or > 240 ||
            request.MaxOtherLectureChanges is < 0 or > 20)
            throw new ArgumentException("راجع حدود أيام الحضور والدقائق والاستراحة؛ حد تحريك محاضرات الآخرين بين صفر و20.");
        var settings = (request.PeriodPreferences ?? []).Select(p => p with { Day = ScheduleDayRules.NormalizeDayName(p.Day) }).ToArray();
        if (settings.Any(p => p.TimeSlotId <= 0 || !Enum.IsDefined(p.Level)) || settings.GroupBy(p => (p.Day, p.TimeSlotId)).Any(g => g.Count() > 1))
            throw new ArgumentException("تفضيلات الفترات مكررة أو غير صالحة.");
        if (request.UsePreferenceMatrix && selected.Length is not (2 or 3))
            throw new ArgumentException("حدد يومين أو ثلاثة أيام للحضور.");
        return request with { SelectedDays = selected, UnavailableDays = request.UsePreferenceMatrix ? ScheduleDayRules.AllDays.Except(selected).ToArray() : unavailable,
            Mode = request.UsePreferenceMatrix ? FacultyAvailabilityMode.Strict : request.Mode,
            PreferredPeriodIds = (request.PreferredPeriodIds ?? []).Distinct().ToArray(), PeriodPreferences = settings };
    }

    public static Dictionary<(int Faculty, int Semester), FacultyScheduleRequest> FromCatalog(ImportCatalog data)
        => FromRows(data.Tables.TryGetValue(typeof(FacultySchedulingPreference), out var rows)
            ? rows.Cast<FacultySchedulingPreference>() : [], data.Tables.TryGetValue(typeof(FacultyPeriodPreference), out var cells)
                ? cells.Cast<FacultyPeriodPreference>() : []);

    public static Dictionary<(int Faculty, int Semester), FacultyScheduleRequest> FromRows(IEnumerable<FacultySchedulingPreference> rows,
        IEnumerable<FacultyPeriodPreference>? periodRows = null)
    {
        var result = new Dictionary<(int, int), FacultyScheduleRequest>();
        foreach (var row in rows)
        {
            FacultyScheduleRequest request;
            try
            {
                request = JsonSerializer.Deserialize<FacultyScheduleRequest>(row.SettingsJson) ?? throw new ArgumentException("ملف تفضيلات فارغ.");
                if (request.UsePreferenceMatrix && periodRows is not null)
                    request = request with { PeriodPreferences = periodRows.Where(p => p.FacultyMemberID == row.FacultyMemberID && p.SemesterNumber == row.SemesterNumber)
                        .Select(p => new FacultyPeriodSetting(p.DayOfWeek, p.TimeSlotID, (FacultyPreferenceLevel)p.Level)).ToArray() };
                request = Normalize(request);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            { throw new InvalidOperationException($"تعذر قراءة تفضيلات التدريسي #{row.FacultyMemberID}؛ أعد حفظها من شاشة أيام التدريسي.", ex); }
            if (request.FacultyId != row.FacultyMemberID || request.Semester != row.SemesterNumber)
                throw new InvalidOperationException("هوية ملف تفضيلات التدريسي غير متطابقة.");
            result.Add((row.FacultyMemberID, row.SemesterNumber), request);
        }
        return result;
    }

    public static FacultyPreferenceLevel Level(FacultyScheduleRequest request, string day, int periodId)
    {
        if (request.UseDayPreferences)
            return request.DayPreferences.SingleOrDefault(p => p.Day == day)?.Level ?? FacultyPreferenceLevel.Prohibited;
        if (!request.SelectedDays.Contains(day)) return FacultyPreferenceLevel.Prohibited;
        return request.PeriodPreferences.SingleOrDefault(p => p.Day == day && p.TimeSlotId == periodId)?.Level
            ?? FacultyPreferenceLevel.Prohibited;
    }
    public static void ValidateMatrix(FacultyScheduleRequest request, IEnumerable<TimeSlot> periods)
    {
        if (!request.UsePreferenceMatrix) return;
        var ids = periods.Where(p => !p.IsBreak).Select(p => p.TimeSlotID).ToHashSet();
        if (ids.Count == 0 || request.PeriodPreferences.Count != ids.Count * ScheduleDayRules.AllDays.Count ||
            request.PeriodPreferences.Any(p => !ids.Contains(p.TimeSlotId) || !request.SelectedDays.Contains(p.Day) && p.Level != FacultyPreferenceLevel.Prohibited))
            throw new ArgumentException("تغيّرت الفترات أو مصفوفة التفضيلات غير مكتملة؛ حدّث الشاشة وراجع كل الفترات، والأيام خارج خطة الحضور يجب أن تكون مرفوضة.");
    }
    public static bool Allows(FacultyScheduleRequest request, Schedule row, TimeSlot period) =>
        (!request.HasScoredPreferences || Level(request, row.DayOfWeek, row.TimeSlotID) != FacultyPreferenceLevel.Prohibited) &&
        !request.UnavailableDays!.Contains(row.DayOfWeek) &&
        (request.Mode != FacultyAvailabilityMode.Strict || request.SelectedDays.Contains(row.DayOfWeek)) &&
        (!request.EarliestStartMinute.HasValue || period.StartTime.TotalMinutes >= request.EarliestStartMinute.Value) &&
        (!request.LatestEndMinute.HasValue || period.EndTime.TotalMinutes <= request.LatestEndMinute.Value);

    public static string? Violation(FacultyScheduleRequest request, IEnumerable<Schedule> source, IReadOnlyDictionary<int, TimeSlot> periods, bool requireAttendance = false)
    {
        var rows = source.Where(s => s.FacultyMemberID == request.FacultyId && s.SemesterNumber == request.Semester).ToList();
        if (request.UsePreferenceMatrix && requireAttendance && rows.Count > 0 &&
            !rows.Select(s => s.DayOfWeek).ToHashSet().SetEquals(request.SelectedDays))
            return "لا يطابق الحضور اليومين أو الأيام الثلاثة المحددة";
        foreach (var row in rows)
            if (!periods.TryGetValue(row.TimeSlotID, out var period) || !Allows(request, row, period))
                return "محاضرة خارج الأيام أو أوقات الإتاحة الإلزامية";
        if (request.MaxTeachingDays.HasValue && rows.Select(s => s.DayOfWeek).Distinct().Count() > request.MaxTeachingDays)
            return "تجاوز الحد الأقصى لأيام الحضور";
        foreach (var day in rows.GroupBy(s => s.DayOfWeek))
        {
            var ordered = day.Select(s => periods[s.TimeSlotID]).OrderBy(p => p.StartTime).ToArray();
            if (request.MaxDailyMinutes.HasValue && ordered.Sum(Duration) > request.MaxDailyMinutes)
                return "تجاوز الحد الأقصى لدقائق التدريس اليومية";
            int running = 0; TimeSpan? end = null;
            foreach (var period in ordered)
            {
                running = !end.HasValue || (period.StartTime - end.Value).TotalMinutes >= request.MinimumRestMinutes
                    ? Duration(period) : running + Duration(period);
                if (request.MaxConsecutiveMinutes.HasValue && running > request.MaxConsecutiveMinutes)
                    return "تجاوز التدريس المتتالي دون الاستراحة المحددة";
                end = period.EndTime;
            }
        }
        return null;
    }
    public static int Duration(TimeSlot p) => checked((int)(p.EndTime - p.StartTime).TotalMinutes);
    public static void Validate(IEnumerable<Schedule> rows, IReadOnlyDictionary<int, TimeSlot> periods,
        IReadOnlyDictionary<(int Faculty, int Semester), FacultyScheduleRequest> profiles)
    {
        var list = rows.ToList();
        foreach (var entry in profiles)
            if (Violation(entry.Value, list, periods, requireAttendance: true) is { } error)
                throw new ArgumentException($"تفضيلات التدريسي #{entry.Key.Faculty}، الفصل {entry.Key.Semester}: {error}. استخدم شاشة أيام التدريسي لإعادة التنظيم أو راجع التفضيلات.");
    }
}
