using ClosedXML.Excel;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public static class ExcelImportTemplates
{
    public static void Create(string path, ExcelImportMode mode)
    {
        using var book = new XLWorkbook();
        foreach (var schema in ExcelImportSchema.For(mode)) AddDataSheet(book, schema, schema.Name, false, mode);
        // Examples are separate, so a new template cannot import demonstration data accidentally.
        foreach (var schema in ExcelImportSchema.For(mode)) AddDataSheet(book, schema, "Example_" + schema.Name, true, mode);
        var instructions = book.AddWorksheet("Instructions");
        string[] lines =
        [
            "University timetable Excel import",
            "Fill the data worksheets. Each row is one record or one lecture. Row 1 contains the column headers.",
            "Headers marked * are required. Example_ sheets are examples only and are never imported.",
            "Keep academic IDs positive and stable across imports. Foreign-key ID columns refer to those same IDs.",
            "Timetable references accept an exact name or #ID, for example #12. A bare number is a name, not an ID.",
            "Use StudyYear and Branch to disambiguate names, or use #ID. SemesterNumber must match the selected semester.",
            "TimeSlot accepts a start time HH:mm or #TimeSlotID. Excel time values are accepted in academic time columns.",
            "LectureType: Theory / Practical (نظري / عملي). Days: Sunday–Thursday or الأحد–الخميس.",
            "Section is a base section, such as A or B. GroupName is a practical subgroup, such as A1/A2 or B1/B2.",
            "Whole-section practical is accepted only when the existing study-year rules permit it. Theory GroupName stays blank.",
            "Hybrid: TRUE in Locked preserves the placement; FALSE allows regeneration. Blank means TRUE in Hybrid, FALSE otherwise.",
            "Locks are saved to the database and respected by later automatic generation. Saved lectures are always preserved by Hybrid.",
            "Hybrid completion covers all subjects and sections of the selected semester and preserves saved lectures.",
            "Academic sheets may be imported together; dependencies are applied before their children.",
            "Existing identical records are unchanged. Academic updates require selecting Allow updates and confirmation.",
            "Timetable import adds entries and never deletes or replaces saved lectures. Conflicting changes need manual correction.",
            "RepairTimetable: imports may conflict. Locked TRUE fixes a row; FALSE or blank lets the solver change day, period or room, retaining teacher/subject/group and duration.",
            "RepairTimetable: optional ScheduleID identifies an existing lecture for placement updates. Without it, an exactly identical saved lecture is matched. Other saved lectures remain fixed.",
            "إصلاح الجدول المستورد: راجع المحاضرات المثبتة ثم تحقق وأصلح، وراجع المقارنة قبل وبعد ثم أكد الحفظ. المحاضرات المحفوظة والمثبتة لا تتحرك.",
            "Use values only; formulas and Excel errors must be corrected before import. Empty rows are ignored.",
            "Academic RoomType: Lecture, Lab, Shared Lab. StartTime/EndTime: HH:mm, at least a 10-minute gap between periods.",
            "The model has no separate Departments table. Groups and weekly requirements follow the existing scheduling rules.",
            "Validate, review every reported error and change, then explicitly confirm saving. A failure rolls back the entire import.",
            "العربية مدعومة. راجع الأخطاء ثم أكد الحفظ. المحاضرات المقفلة تبقى في أماكنها أثناء التوليد الهجين."
        ];
        for (int i = 0; i < lines.Length; i++) instructions.Cell(i + 1, 1).Value = lines[i];
        instructions.Column(1).Width = 120;
        instructions.Column(1).Style.Alignment.WrapText = true;
        instructions.Rows(1, lines.Length).Height = 34;
        instructions.Row(1).Style.Font.Bold = true;
        instructions.Row(1).Style.Font.FontSize = 16;
        instructions.SheetView.FreezeRows(1);
        book.SaveAs(path);
    }
    private static void AddDataSheet(XLWorkbook book, ImportSheetSchema schema, string name, bool example, ExcelImportMode mode)
    {
        var sheet = book.AddWorksheet(name);
        var columns = schema.Columns.ToArray();
        for (int c = 0; c < columns.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c] + (schema.Required.Contains(columns[c]) ? " *" : "");
            sheet.Column(c + 1).Width = Math.Max(18, Math.Min(28, columns[c].Length + 5));
        }
        var header = sheet.Range(1, 1, 1, columns.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        header.Style.Font.FontColor = XLColor.FromHtml("#18263E");
        header.Style.Font.Bold = true;
        header.Style.Alignment.WrapText = true;
        sheet.Row(1).Height = 32;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, 2, columns.Length).SetAutoFilter();
        sheet.Style.Font.FontName = "Segoe UI";
        sheet.Style.Font.FontSize = 11;
        if (!example) return;
        var values = Sample(schema.Name, mode);
        for (int c = 0; c < columns.Length; c++)
            if (values.TryGetValue(columns[c], out var value)) sheet.Cell(2, c + 1).Value = XLCellValue.FromObject(value);
        sheet.Row(2).Height = 26;
    }
    private static Dictionary<string, object> Sample(string sheet, ExcelImportMode mode) => sheet switch
    {
        "StudyYears" => new() { ["StudyYearID"] = 1, ["YearName"] = "First Year" },
        "Branches" => new() { ["BranchID"] = 1, ["BranchName"] = "Computer Science" },
        "FacultyMembers" => new() { ["FacultyMemberID"] = 1, ["FullName"] = "أحمد محمد", ["AcademicTitle"] = "Lecturer" },
        "Classrooms" => new() { ["ClassroomID"] = 1, ["ClassroomNumber"] = "Room 101", ["Capacity"] = 60, ["RoomType"] = "Lecture" },
        "TimeSlots" => new() { ["TimeSlotID"] = 1, ["StartTime"] = "08:30", ["EndTime"] = "10:00" },
        "Sections" => new() { ["SectionID"] = 1, ["SectionName"] = "A", ["StudentCount"] = 40, ["StudyYearID"] = 1 },
        "Subjects" => new() { ["SubjectID"] = 1, ["SubjectName"] = "الرياضيات", ["StudyYearID"] = 1, ["SemesterNumber"] = 1, ["TheoreticalHours"] = 2, ["PracticalHours"] = 0, ["CreditUnits"] = 2 },
        "FacultyMemberSubjects" => new() { ["FacultyMemberID"] = 1, ["SubjectID"] = 1 },
        _ => new() { ["Day"] = "Sunday", ["TimeSlot"] = "#1", ["Subject"] = "#1", ["Faculty"] = "#1", ["Classroom"] = "#1", ["Section"] = "#1", ["LectureType"] = "Theory", ["SemesterNumber"] = 1, ["Locked"] = mode == ExcelImportMode.Hybrid }
    };
}
