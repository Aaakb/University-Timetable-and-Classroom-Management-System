using System.Data;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

// Import names as independent records, never as teaching assignments.
internal static class NamedAssignmentImport
{
    public static bool IsMatch(DataTable table)
    {
        bool Has(string field) => table.Columns.Cast<DataColumn>().Any(c => ImportLabels.MatchColumn(c.ColumnName, [field]) is not null);
        return (Has("FacultyName") || Has("SubjectName")) && !Has("FacultyMemberID") && !Has("SubjectID") && !Has("Day");
    }

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
            // ID-based sheets keep their own schema and validation.
            if (source.Name != ExcelImportSchema.NamedAssignments.Name && source.SuggestedSchemaName != ExcelImportSchema.NamedAssignments.Name && !IsMatch(source.Table))
            { output.Add(source); continue; }
            var map = source.Table.Columns.Cast<DataColumn>().ToDictionary(c => c.ColumnName,
                c => ImportLabels.MatchColumn(c.ColumnName, ExcelImportSchema.NamedAssignments.Columns) ?? "");
            var sheet = source.MapColumns(map);
            if (!sheet.Table.Columns.Contains("FacultyName") && !sheet.Table.Columns.Contains("SubjectName"))
            { preview.Issues.Add(new(source.SourceName, 1, "", "يجب وجود عمود اسم التدريسي أو اسم المادة.")); continue; }
            var teachers = Table("FacultyMembers", "FacultyMemberID", "FullName", "AcademicTitle");
            var materials = Table("Subjects", "SubjectID", "SubjectName", "StudyYearID", "BranchID", "SemesterNumber", "TheoreticalHours", "PracticalHours", "CreditUnits");
            var teacherRows = new List<int>(); var materialRows = new List<int>();
            for (int i = 0; i < sheet.Table.Rows.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                int row = sheet.SourceRows[i];
                string Get(string field) => sheet.Table.Columns.Contains(field) ? Convert.ToString(sheet.Table.Rows[i][field])?.Trim() ?? "" : "";
                try
                {
                    string session = ImportLabels.Normalize(Get("StudySession"));
                    if (session is "مسائي" or "evening")
                    { preview.ReadingNotes.Add(new(source.SourceName, row, "StudySession", "استُبعد الصف المسائي؛ البرنامج مخصص للدراسة الصباحية.")); continue; }
                    if (session is not ("" or "صباحي" or "morning")) throw new ArgumentException("الدراسة يجب أن تكون صباحي أو مسائي.");
                    string name = Get("FacultyName"), subjectName = Get("SubjectName");
                    if (name.Length == 0 && subjectName.Length == 0) throw new ArgumentException("أدخل اسم التدريسي أو اسم المادة.");
                    Subject? subject = null;
                    bool newSubject = false;
                    if (subjectName.Length > 0)
                    {
                        int year = Year(ImportLabels.NormalizeReference(Get("StudyYear")));
                        if (year == 0) throw new ArgumentException("حدد مرحلة المادة: الأولى أو الثانية أو الثالثة أو الرابعة.");
                        string branch = Branch(Get("Branch"));
                        int? branchId = null;
                        if (year >= 3)
                        {
                            var branches = catalog.Rows<Branch>().Where(b => Branch(b.BranchName) == branch).ToList();
                            if (branches.Count != 1) throw new ArgumentException("حدد تخصص المادة الموجود: CS أو CYS أو IT.");
                            branchId = branches[0].BranchID;
                        }
                        else if (branch is not ("" or "عام" or "general" or "-"))
                            throw new ArgumentException("الأولى والثانية مواد عامة؛ راجع خانة التخصص.");
                        int? semester = null;
                        if (Get("SemesterNumber").Length > 0)
                        {
                            if (!int.TryParse(ImportLabels.NormalizeReference(Get("SemesterNumber")), out int term) || term is not (1 or 2))
                                throw new ArgumentException("الفصل الدراسي يجب أن يكون 1 أو 2.");
                            semester = term;
                        }
                        var matches = subjects.Where(s => !s.IsDeleted && s.StudyYearID == year && s.BranchID == branchId &&
                            ImportLabels.Normalize(s.SubjectName) == ImportLabels.Normalize(subjectName) && (!semester.HasValue || s.SemesterNumber == semester)).ToList();
                        if (matches.Count > 1) throw new ArgumentException("المادة لها أكثر من تطابق؛ حدد الفصل الدراسي 1 أو 2.");
                        subject = matches.SingleOrDefault();
                        if (subject is null)
                        {
                            if (!semester.HasValue) throw new ArgumentException("المادة جديدة؛ حدد الفصل الدراسي 1 أو 2 لإضافتها.");
                            if (!catalog.Rows<StudyYear>().Any(y => y.StudyYearID == year)) throw new ArgumentException("مرحلة المادة غير موجودة في البرنامج.");
                            double Number(string field) => Get(field).Length == 0 ? 0 : new ImportValues(sheet.Table.Rows[i]).Number(field);
                            subject = new Subject { SubjectID = checked(nextSubject + 1), SubjectName = subjectName, StudyYearID = year,
                                BranchID = branchId, SemesterNumber = semester.Value, TheoreticalHours = Number("TheoreticalHours"),
                                PracticalHours = Number("PracticalHours"), CreditUnits = Number("CreditUnits") };
                            InputValidation.TextLength(subjectName, 100, "SubjectName");
                            newSubject = true;
                        }
                    }
                    if (name.Length > 0)
                    {
                        var matches = faculty.Where(f => ImportLabels.Normalize(f.FullName) == ImportLabels.Normalize(name)).ToList();
                        if (matches.Count > 1) throw new ArgumentException($"اسم التدريسي «{name}» يطابق أكثر من سجل.");
                        if (matches.Count == 0)
                        {
                            InputValidation.TextLength(name, 100, "FullName");
                            InputValidation.TextLength(Get("AcademicTitle"), 50, "AcademicTitle");
                            var teacher = new FacultyMember { FacultyMemberID = checked(++nextFaculty), FullName = name, AcademicTitle = Get("AcademicTitle") };
                            faculty.Add(teacher); teachers.Rows.Add(teacher.FacultyMemberID, name, teacher.AcademicTitle); teacherRows.Add(row);
                        }
                    }
                    if (newSubject)
                    {
                        nextSubject = subject!.SubjectID; subjects.Add(subject);
                        materials.Rows.Add(subject.SubjectID, subject.SubjectName, subject.StudyYearID, subject.BranchID?.ToString() ?? "",
                            subject.SemesterNumber, subject.TheoreticalHours, subject.PracticalHours, subject.CreditUnits); materialRows.Add(row);
                    }
                    preview.ReadingNotes.Add(new(source.SourceName, row, "", "تمت قراءة التدريسي والمادة كسجلات مستقلة دون إنشاء تكليف؛ السجلات الموجودة لا تُكرر."));
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
    private static int Year(string value) => ImportLabels.Normalize(value) switch
    {
        "1" or "الاولي" or "المرحلةالاولي" or "firstyear" => 1,
        "2" or "الثانية" or "المرحلةالثانية" or "secondyear" => 2,
        "3" or "الثالثة" or "المرحلةالثالثة" or "thirdyear" => 3,
        "4" or "الرابعة" or "المرحلةالرابعة" or "fourthyear" => 4, _ => 0
    };
    private static string Branch(string value) => ImportLabels.Normalize(value) switch
    {
        "علومالحاسوب" or "علومالحاسبات" or "computerscience" or "cs" => "CS",
        "الامنالسيبراني" or "cybersecurity" or "cys" => "CYS",
        "تكنولوجياالمعلومات" or "تقنيةالمعلومات" or "informationtechnology" or "it" => "IT",
        var other => other
    };
}
