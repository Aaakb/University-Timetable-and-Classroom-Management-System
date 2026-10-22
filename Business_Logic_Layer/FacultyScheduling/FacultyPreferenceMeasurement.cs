using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public sealed record FacultyPreferenceMeasurement(string Faculty, int TeachingMinutes,
    int BeforeStronglyPreferredMinutes, int AfterStronglyPreferredMinutes,
    int BeforePreferredMinutes, int AfterPreferredMinutes, int BeforeProhibitedMinutes,
    int AfterProhibitedMinutes, decimal BeforePenalty, decimal AfterPenalty,
    double StronglyPreferredPercentage);

internal static class FacultyPreferenceMeasurements
{
    public static IReadOnlyList<FacultyPreferenceMeasurement> Compare(ImportCatalog data,
        IEnumerable<Schedule> before, IEnumerable<Schedule> after, FacultyScheduleRequest request)
    {
        var profiles = FacultyPreferenceRules.FromCatalog(data);
        profiles[(request.FacultyId, request.Semester)] = request;
        var periods = data.Rows<TimeSlot>().ToDictionary(p => p.TimeSlotID);
        var names = data.Rows<FacultyMember>().ToDictionary(f => f.FacultyMemberID, f => f.FullName);
        var oldRows = before.ToList(); var newRows = after.ToList();
        return profiles.Where(p => p.Value.HasScoredPreferences && p.Key.Semester == request.Semester).Select(entry =>
        {
            int Minutes(List<Schedule> rows, FacultyPreferenceLevel level) => rows.Where(s => s.FacultyMemberID == entry.Key.Faculty &&
                s.SemesterNumber == entry.Key.Semester && FacultyPreferenceRules.Level(entry.Value, s.DayOfWeek, s.TimeSlotID) == level)
                .Sum(s => FacultyPreferenceRules.Duration(periods[s.TimeSlotID]));
            int oldStrong = Minutes(oldRows, FacultyPreferenceLevel.StronglyPreferred), strong = Minutes(newRows, FacultyPreferenceLevel.StronglyPreferred);
            int oldPreferred = Minutes(oldRows, FacultyPreferenceLevel.Preferred), preferred = Minutes(newRows, FacultyPreferenceLevel.Preferred);
            int denied = Minutes(newRows, FacultyPreferenceLevel.Prohibited);
            int total = strong + preferred + denied;
            return new FacultyPreferenceMeasurement(names[entry.Key.Faculty], total, oldStrong, strong, oldPreferred, preferred,
                Minutes(oldRows, FacultyPreferenceLevel.Prohibited), denied, Math.Round(oldPreferred / 30m, 2), Math.Round(preferred / 30m, 2),
                total == 0 ? 0 : Math.Round(100d * strong / total, 1));
        }).ToArray();
    }
}
