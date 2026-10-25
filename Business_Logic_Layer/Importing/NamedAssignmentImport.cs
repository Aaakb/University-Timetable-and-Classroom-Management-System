using System.Data;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

// Only names are imported. Academic scope is completed later in the subject editor.
internal static class NamedAssignmentImport
{
    public static bool IsMatch(DataTable table) => table.Columns.Cast<DataColumn>().Any(c =>
        ImportLabels.MatchColumn(c.ColumnName, ["FacultyName", "SubjectName"]) is not null);

    public static IReadOnlyList<ExcelSheetData> Expand(IReadOnlyList<ExcelSheetData> sheets, ImportCatalog catalog,
        ExcelImportPreview preview, CancellationToken token)
    {
        var output = new List<ExcelSheetData>();
        var faculty = catalog.Rows<FacultyMember>();
        var subjects = catalog.Rows<Subject>();
        int nextFaculty = faculty.Select(f => f.FacultyMemberID).DefaultIfEmpty().Max();
        int nextSubject = subjects.Select(s => s.SubjectID).DefaultIfEmpty().Max();
        foreach (var source in sheets)
        {
            token.ThrowIfCancellationRequested();
            if (source.Name != ExcelImportSchema.NamedAssignments.Name && source.SuggestedSchemaName != ExcelImportSchema.NamedAssignments.Name && !IsMatch(source.Table))
            { output.Add(source); continue; }
            var map = source.Table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName,
                c => ImportLabels.MatchColumn(c.ColumnName, ExcelImportSchema.NamedAssignments.Columns) ?? "");
            var sheet = source.MapColumns(map);
            if (!sheet.Table.Columns.Contains("FacultyName") && !sheet.Table.Columns.Contains("SubjectName"))
            { preview.Issues.Add(new(source.SourceName, 1, "", "يجب وجود عمود اسم التدريسي أو اسم المادة.")); continue; }
            var teachers = Table("FacultyMembers", "FacultyMemberID", "FullName");
            var materials = Table("Subjects", "SubjectID", "SubjectName", "StudyYearID", "SemesterNumber", "TheoreticalHours", "PracticalHours", "TheoreticalLectureCount", "PracticalLectureCount");
            var teacherRows = new List<int>(); var materialRows = new List<int>();
            for (int i = 0; i < sheet.Table.Rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                int row = sheet.SourceRows[i];
                string Get(string field) => sheet.Table.Columns.Contains(field) ? Convert.ToString(sheet.Table.Rows[i][field])?.Trim() ?? "" : "";
                try
                {
                    string name = Get("FacultyName"), subjectName = Get("SubjectName");
                    if (name.Length == 0 && subjectName.Length == 0) throw new ArgumentException("أدخل اسم التدريسي أو اسم المادة.");
                    InputValidation.TextLength(name, 100, "FullName");
                    InputValidation.TextLength(subjectName, 100, "SubjectName");
                    bool added = false;
                    if (name.Length > 0 && !faculty.Any(f => ImportLabels.Normalize(f.FullName) == ImportLabels.Normalize(name)))
                    {
                        var teacher = new FacultyMember { FacultyMemberID = checked(++nextFaculty), FullName = name };
                        faculty.Add(teacher); teachers.Rows.Add(teacher.FacultyMemberID, name); teacherRows.Add(row); added = true;
                    }
                    if (subjectName.Length > 0 && !subjects.Any(s => !s.IsDeleted && ImportLabels.Normalize(s.SubjectName) == ImportLabels.Normalize(subjectName)))
                    {
                        var subject = new Subject { SubjectID = checked(++nextSubject), SubjectName = subjectName,
                            StudyYearID = Subject.UnclassifiedStudyYearId, SemesterNumber = 1, TheoreticalLectureCount = 0, PracticalLectureCount = 0 };
                        subjects.Add(subject);
                        materials.Rows.Add(subject.SubjectID, subjectName, subject.StudyYearID, subject.SemesterNumber, 0, 0, 0, 0);
                        materialRows.Add(row); added = true;
                    }
                    preview.ReadingNotes.Add(new(source.SourceName, row, "", added
                        ? "إضافة الأسماء الجديدة فقط دون إنشاء تكليفات. يمكن استكمال بيانات المادة لاحقاً من شاشة المواد."
                        : "تم تخطي الأسماء الموجودة مسبقاً دون تكرارها أو تعديلها."));
                }
                catch (Exception ex) when (ex is ArgumentException or OverflowException)
                { preview.Issues.Add(new(source.SourceName, row, "", ex.Message)); }
            }
            if (teachers.Rows.Count > 0) output.Add(new("FacultyMembers", teachers, teacherRows) { SourceName = source.SourceName });
            if (materials.Rows.Count > 0) output.Add(new("Subjects", materials, materialRows) { SourceName = source.SourceName });
        }
        return output;
    }
    private static DataTable Table(string name, params string[] columns)
    { var table = new DataTable(name); foreach (string column in columns) table.Columns.Add(column, typeof(string)); return table; }
}
