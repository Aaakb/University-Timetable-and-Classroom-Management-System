using System.Text;
using System.Text.RegularExpressions;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

public static class ImportLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StudyYears"] = "المراحل الدراسية", ["Branches"] = "الفروع", ["FacultyMembers"] = "التدريسيون",
        ["NamedAssignments"] = "التدريسيون والمواد بالأسماء (دون تكليفات)", ["Skipped"] = "تم التخطي",
        ["Classrooms"] = "القاعات", ["TimeSlots"] = "الفترات الزمنية", ["Sections"] = "الشعب",
        ["Subjects"] = "المواد الدراسية", ["FacultyMemberSubjects"] = "تكليفات التدريس", ["Timetable"] = "الجدول الدراسي",
        ["StudyYearID"] = "معرف المرحلة", ["YearName"] = "اسم المرحلة", ["BranchID"] = "معرف الفرع",
        ["BranchName"] = "اسم الفرع", ["FacultyMemberID"] = "معرف التدريسي", ["FullName"] = "الاسم الكامل",
        ["AcademicTitle"] = "اللقب العلمي", ["ClassroomID"] = "معرف القاعة", ["ClassroomNumber"] = "رقم القاعة",
        ["Capacity"] = "السعة", ["RoomType"] = "نوع القاعة", ["TimeSlotID"] = "معرف الفترة",
        ["StartTime"] = "وقت البدء", ["EndTime"] = "وقت الانتهاء", ["SectionID"] = "معرف الشعبة",
        ["SectionName"] = "اسم الشعبة", ["StudentCount"] = "عدد الطلبة", ["SubjectID"] = "معرف المادة",
        ["SubjectName"] = "اسم المادة", ["SemesterNumber"] = "الفصل الدراسي", ["TheoreticalHours"] = "الساعات النظرية",
        ["PracticalHours"] = "الساعات العملية", ["CreditUnits"] = "الوحدات", ["Day"] = "اليوم",
        ["TheoreticalLectureCount"] = "عدد المحاضرات النظرية أسبوعياً", ["PracticalLectureCount"] = "عدد المحاضرات العملية أسبوعياً لكل مجموعة",
        ["DayOfWeek"] = "اليوم", ["TimeSlot"] = "الفترة", ["Subject"] = "المادة", ["Faculty"] = "التدريسي",
        ["Classroom"] = "القاعة", ["Section"] = "الشعبة", ["LectureType"] = "نوع المحاضرة",
        ["GroupName"] = "المجموعة", ["StudyYear"] = "المرحلة", ["Branch"] = "الفرع", ["Locked"] = "تثبيت المحاضرة",
        ["Sheet"] = "المصدر", ["Row"] = "الصف", ["Status"] = "الحالة", ["Detail"] = "التفاصيل",
        ["FacultyName"] = "اسم التدريسي", ["ClassroomName"] = "القاعة", ["Period"] = "الفترة",
        ["Add"] = "إضافة", ["Update"] = "تحديث", ["Unchanged"] = "دون تغيير", ["Lock existing"] = "تثبيت محاضرة موجودة",
        ["Error"] = "خطأ", ["Unscheduled"] = "لم تُجدول", ["Generated"] = "المحاضرات المولّدة",
        ["Saved"] = "المحاضرات المحفوظة", ["Section/group"] = "الشعبة أو المجموعة",
        ["ScheduleID"] = "معرف المحاضرة المحفوظة", ["Needs repair"] = "قابل للإصلاح",
        ["BeforeDay"] = "اليوم قبل", ["BeforePeriod"] = "الفترة قبل", ["BeforeClassroom"] = "القاعة قبل",
        ["AfterDay"] = "اليوم بعد", ["AfterPeriod"] = "الفترة بعد", ["AfterClassroom"] = "القاعة بعد", ["Changed"] = "تغيّر الموضع",
        ["BeforePlacement"] = "الموضع قبل الإصلاح", ["AfterPlacement"] = "الموضع بعد الإصلاح",
        ["StudySession"] = "الدراسة / الدوام", ["SourceText"] = "النص الأصلي للخانة", ["ImportNote"] = "ملاحظات القراءة", ["Review source"] = "مراجعة المصدر",
        ["Sunday"] = "الأحد", ["Monday"] = "الاثنين", ["Tuesday"] = "الثلاثاء", ["Wednesday"] = "الأربعاء",
        ["Thursday"] = "الخميس", ["Friday"] = "الجمعة", ["Saturday"] = "السبت", ["Theory"] = "نظري", ["Practical"] = "عملي"
    };
    public static string Display(string value) => Labels.GetValueOrDefault(value, value);

    public static string? MatchColumn(string header, IEnumerable<string> columns)
    {
        string normalized = Normalize(header);
        return columns.FirstOrDefault(column => Normalize(column) == normalized || Normalize(Display(column)) == normalized ||
            (column == "SubjectName" && normalized == Normalize("المادة")) ||
            (column == "Branch" && normalized == Normalize("التخصص")) ||
            (column == "StudySession" && normalized == Normalize("الدراسة")));
    }

    public static string? MatchSheet(string name) => ExcelImportSchema.AcademicInputs
        .Select(s => s.Name).FirstOrDefault(s => Normalize(s) == Normalize(name) || Normalize(Display(s)) == Normalize(name));

    // Normalize harmless differences introduced by Arabic documents. Callers still
    // reject multiple matches, so normalization never silently picks a person/material.
    public static string NormalizeReference(string value)
    {
        string text = value.Normalize(NormalizationForm.FormKC).Replace('ی', 'ي').Replace('ک', 'ك');
        text = Regex.Replace(text, @"[\p{Cf}\u0640\u064b-\u065f\u0670]", "");
        text = new string(text.Select(c => c is >= '\u0660' and <= '\u0669' ? (char)('0' + c - '\u0660') :
            c is >= '\u06f0' and <= '\u06f9' ? (char)('0' + c - '\u06f0') : c).ToArray());
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    public static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC)
        .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ى', 'ي'), @"[\s\p{M}\p{Cf}_*\-]+", "").ToLowerInvariant();
}
