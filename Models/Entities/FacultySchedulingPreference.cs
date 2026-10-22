namespace University_Timetable_and_Classroom_Management_System.Models;

// One explicit preference profile per faculty member and semester. The JSON is
// versioned by the business layer; it contains no lecture placements.
public sealed class FacultySchedulingPreference
{
    public int FacultyMemberID { get; set; }
    public int SemesterNumber { get; set; }
    public string SettingsJson { get; set; } = "";
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
