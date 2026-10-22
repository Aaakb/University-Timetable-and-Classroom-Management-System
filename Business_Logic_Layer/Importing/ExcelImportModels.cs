using System.Data;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public enum ExcelImportMode { AcademicData, CompleteTimetable, Hybrid, RepairTimetable }
public sealed record ImportIssue(string Sheet, int Row, string Column, string Message);
public sealed record ImportChange(string Sheet, int Row, string Action, string Key);
public sealed record ImportPlacementChange(string Sheet, int Row, string SubjectName, string FacultyName,
    string SectionName, string LectureType, string? GroupName, bool Locked,
    string BeforeDay, string BeforePeriod, string BeforeClassroom,
    string AfterDay, string AfterPeriod, string AfterClassroom, bool Changed);
public sealed class ExcelSheetData
{
    public string Name { get; }
    public string SourceName { get; init; }
    public DataTable Table { get; }
    public string? SuggestedSchemaName { get; init; }
    public bool IsWeeklyTimetable { get; init; }
    public string StudySession { get; init; } = "";
    // Excel row numbers or PDF text-line numbers are retained for source diagnostics.
    public IReadOnlyList<int> SourceRows { get; }
    public ExcelSheetData(string name, DataTable table, IReadOnlyList<int> sourceRows)
        => (Name, SourceName, Table, SourceRows) = (name, name, table, sourceRows);
    public ExcelSheetData MapColumns(IReadOnlyDictionary<string, string> mapping)
    {
        var copy = new DataTable(Name) { Locale = Table.Locale };
        var included = new List<int>();
        foreach (DataColumn column in Table.Columns)
        {
            string target = mapping.TryGetValue(column.ColumnName, out var mapped) ? mapped : column.ColumnName;
            if (string.IsNullOrWhiteSpace(target)) continue;
            copy.Columns.Add(target, typeof(string));
            included.Add(column.Ordinal);
        }
        foreach (DataRow row in Table.Rows) copy.Rows.Add(included.Select(i => row[i]).ToArray());
        return new(Name, copy, SourceRows.ToArray()) { SuggestedSchemaName = SuggestedSchemaName, SourceName = SourceName, IsWeeklyTimetable = IsWeeklyTimetable, StudySession = StudySession };
    }
}
public sealed class ExcelWorkbookData
{
    public List<ExcelSheetData> Sheets { get; } = [];
}
public sealed record TimetableImportRow(string Sheet, int Row, Schedule Schedule, bool Locked)
{
    public string SubjectName { get; init; } = "";
    public string FacultyName { get; init; } = "";
    public string ClassroomName { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string Period { get; init; } = "";
}
public sealed class ExcelImportPreview
{
    public List<ImportIssue> Issues { get; } = [];
    public List<ImportChange> Changes { get; } = [];
    public List<TimetableImportRow> Lectures { get; } = [];
    public List<string> UnscheduledLectures { get; } = [];
    public List<ImportIssue> RepairableConflicts { get; } = [];
    public List<ImportIssue> ReadingNotes { get; } = [];
    public List<ImportPlacementChange> PlacementChanges { get; } = [];
    public string RepairSearchStatus { get; internal set; } = "";
    public bool IsValid => Issues.Count == 0;
    public int RemainingLectureCount { get; internal set; }
    public bool GenerationCompleted { get; internal set; }
    internal List<AcademicImportRow> AcademicRows { get; } = [];
    internal List<TimetableImportRow> RepairOriginals { get; } = [];
    internal string DatabaseVersion { get; set; } = "";
    internal string ContentVersion { get; set; } = "";
    internal ExcelImportMode Mode { get; set; }
    internal bool UpdateExisting { get; set; }
    internal int Semester { get; set; }
}
internal sealed record AcademicImportRow(string Sheet, int Row, object Entity, string Action, string Key);
public sealed record ImportSaveResult(int Added, int Updated, int Unchanged);
