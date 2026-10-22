using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public enum FacultyAvailabilityMode { Strict, Preferred }
public enum ScheduleOptimizationGoal { MinimumChanges, CompactAttendance, FewerGaps, BalancedStudentLoad }
public enum FacultyPreferenceLevel { StronglyPreferred = 0, Preferred = 1, Prohibited = 2 }
public sealed record FacultyPeriodSetting(string Day, int TimeSlotId, FacultyPreferenceLevel Level);
public sealed record FacultyDaySetting(string Day, FacultyPreferenceLevel Level);
public sealed record FacultyScheduleRequest(int FacultyId, int Semester, FacultyAvailabilityMode Mode,
    IReadOnlyList<string> SelectedDays, IReadOnlyList<string>? UnavailableDays = null,
    IReadOnlyList<int>? PreferredPeriodIds = null, double TimeLimitSeconds = 20)
{
    public int? EarliestStartMinute { get; init; }
    public int? LatestEndMinute { get; init; }
    public int? TargetTeachingDays { get; init; }
    public int? MaxTeachingDays { get; init; }
    public int? MaxDailyMinutes { get; init; }
    public int? MaxConsecutiveMinutes { get; init; }
    public int MinimumRestMinutes { get; init; } = 20;
    public ScheduleOptimizationGoal Goal { get; init; }
    // Zero is the default: moving another faculty's lectures is always explicit.
    public int MaxOtherLectureChanges { get; init; }
    public bool UsePreferenceMatrix { get; init; }
    public IReadOnlyList<FacultyPeriodSetting> PeriodPreferences { get; init; } = [];
    public bool UseDayPreferences { get; init; }
    public IReadOnlyList<FacultyDaySetting> DayPreferences { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasScoredPreferences => UseDayPreferences || UsePreferenceMatrix;
}
public sealed record FacultyDayPreferenceResult(FacultyPreferenceProfile Profile, int ChangedLectures,
    string Status, IReadOnlyList<FacultyLectureChange> Changes);
public sealed record FacultyPreferenceProfile(FacultyScheduleRequest Settings, long Revision);
public sealed record ScheduleQuality(int Lectures, int ChangedLectures, int OtherFacultyChanges,
    int TeachingDays, int FacultyGapMinutes, int StudentGapMinutes, int OutsidePreferredDays,
    int OutsidePreferredPeriods, int PeakStudentDailyMinutes, double RoomSeatUtilizationPercent);
public sealed record FacultyScheduleAlternative(string Title, FacultySchedulePreview Preview);
public sealed record FacultyEditorOption(int Id, string Name);
public sealed record FacultyEditorLookups(IReadOnlyList<FacultyEditorOption> Faculty,
    IReadOnlyList<string> Days, IReadOnlyList<FacultyEditorOption> Periods);
public sealed record FacultyLectureView(int ScheduleID, string Subject, string Section, string LectureType,
    string? GroupName, string Day, string Period, string Room);
public sealed record FacultyLectureChange(int ScheduleID, string Subject, string Section, string LectureType,
    string? GroupName, string OldDay, string OldPeriod, string OldRoom,
    string NewDay, string NewPeriod, string NewRoom, bool Changed, bool OutsidePreferredDays)
{
    public string Faculty { get; init; } = "";
    public bool IsOtherFaculty { get; init; }
}
public sealed record FacultyAvailableSlot(int ScheduleID, string Subject, string Day, string Period, string Room, bool PreferredDay);
public sealed record FacultySlotSummary(int ScheduleID, string Subject, int PreferredSlots, int OtherSlots);
public sealed class FacultySchedulePreview
{
    public bool CanSave => Plan is not null;
    public string Status { get; internal init; } = "";
    public string Message { get; internal init; } = "";
    public IReadOnlyList<FacultyLectureChange> Changes { get; internal init; } = [];
    public IReadOnlyList<FacultySlotSummary> AvailableSlots { get; internal init; } = [];
    public IReadOnlyList<FacultyAvailableSlot> SlotOptions { get; internal init; } = [];
    public IReadOnlyList<string> Suggestions { get; internal init; } = [];
    public ScheduleQuality? BeforeQuality { get; internal init; }
    public ScheduleQuality? AfterQuality { get; internal init; }
    public IReadOnlyList<FacultyPreferenceMeasurement> PreferenceMeasurements { get; internal init; } = [];
    public double SearchSeconds { get; internal init; }
    public int? HardConstraintViolations => CanSave ? 0 : null;
    public decimal TotalPreferencePenaltyBefore => PreferenceMeasurements.Sum(m => m.BeforePenalty);
    public decimal TotalPreferencePenaltyAfter => PreferenceMeasurements.Sum(m => m.AfterPenalty);
    public int AffectedFacultyCount { get; internal init; }
    internal string Version { get; init; } = "";
    internal FacultyScheduleRequest Request { get; init; } = null!;
    internal IReadOnlyList<Schedule>? Plan { get; init; }
}
