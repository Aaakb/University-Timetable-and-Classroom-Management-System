using System.Text.RegularExpressions;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace University_Timetable_and_Classroom_Management_System;

/// <summary>Localizes import diagnostics for the Arabic form without changing service contracts.</summary>
internal static class ImportUiText
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Select an .xlsx workbook."] = "اختر ملف Excel بصيغة .xlsx.",
        ["The workbook has no import worksheets."] = "الملف لا يحتوي أوراق بيانات قابلة للاستيراد.",
        ["Select semester 1 or 2."] = "اختر الفصل الدراسي الأول أو الثاني.",
        ["Select at least one worksheet containing data."] = "اختر ورقة أو صفحة تحتوي بيانات.",
        ["Required column is missing. Map the source column or use the template."] = "عمود مطلوب غير موجود. اربط العمود المقابل أو استخدم القالب.",
        ["SemesterNumber differs from the selected import semester."] = "الفصل الدراسي في الصف يختلف عن الفصل المحدد للاستيراد.",
        ["Theory lectures cover a whole section; GroupName must be empty."] = "المحاضرة النظرية تشمل الشعبة كاملة؛ اترك المجموعة فارغة.",
        ["Duplicate lecture in this workbook."] = "المحاضرة مكررة داخل الملف.",
        ["LectureType: use Theory or Practical (نظري / عملي)."] = "نوع المحاضرة يجب أن يكون نظرياً أو عملياً.",
        ["This lecture exceeds the existing engine's weekly requirement for this section/group, including saved lectures."] = "هذه المحاضرة تتجاوز المتطلبات الأسبوعية للشعبة أو المجموعة، مع احتساب المحاضرات المحفوظة.",
        ["Select Hybrid mode."] = "اختر وضع الجدولة الهجينة.",
        ["Database data changed. Validate the workbook again."] = "تغيّرت بيانات قاعدة البيانات. أعد التحقق من الملف.",
        ["A saved lecture requires correction before hybrid generation. Validate the existing timetable first."] = "توجد محاضرة محفوظة تحتاج إلى تصحيح. تحقق من الجدول الحالي قبل التوليد.",
        ["The search did not complete the timetable. Review the unresolved lectures; no data has been saved."] = "لم يكتمل الجدول ضمن حدود البحث. راجع المحاضرات المتبقية؛ لم تُحفظ البيانات.",
        ["No lecture requirements exist for the selected semester."] = "لا توجد متطلبات محاضرات للفصل المحدد.",
        ["The preview is invalid or changed. Run validation again."] = "المعاينة غير صالحة أو تغيرت. أعد التحقق من البيانات.",
        ["Explicit import confirmation is required."] = "يجب تأكيد الاستيراد قبل الحفظ.",
        ["Generate and review the completed timetable before saving."] = "ولّد الجدول المكتمل وراجعه قبل الحفظ.",
        ["Database data changed after preview. Validate again before saving."] = "تغيّرت قاعدة البيانات بعد المعاينة. أعد التحقق قبل الحفظ.",
        ["A saved lecture no longer exists. Validate again."] = "إحدى المحاضرات المحفوظة لم تعد موجودة. أعد التحقق.",
        ["Duplicate key in this workbook."] = "المعرف مكرر داخل الملف.",
        ["Existing record differs. Enable updates explicitly to change it."] = "السجل يختلف عن السجل الموجود. فعّل السماح بالتحديث لتعديله.",
        ["Unknown academic worksheet. Select a supported entity type or use a standard template."] = "نوع البيانات غير معروف. ألغِ استيراد جميع الجداول وحدد نوع البيانات، أو استخدم القالب.",
        ["IDs must be positive integers. Keep IDs stable for re-import."] = "يجب أن تكون المعرفات أعداداً صحيحة موجبة وثابتة عند إعادة الاستيراد.",
        ["Capacity must be greater than zero."] = "يجب أن تكون سعة القاعة أكبر من صفر.",
        ["RoomType must be Lecture, Lab or Shared Lab."] = "نوع القاعة: Lecture للمحاضرات، أو Lab للمختبر، أو Shared Lab للمختبر المشترك.",
        ["Lecture periods overlap or have less than a 10-minute gap."] = "الفترات متداخلة أو الفاصل بينها أقل من عشر دقائق.",
        ["StudentCount cannot be negative."] = "عدد الطلبة لا يمكن أن يكون سالباً.",
        ["End time must be after start time."] = "يجب أن يكون وقت الانتهاء بعد وقت البدء.",
        ["Choose start and end times within the same day, using whole minutes. Lecture duration is flexible."] = "اختر وقتي البدء والانتهاء في اليوم نفسه بدقائق كاملة.",
        ["Lecture periods must not overlap and must leave at least a 10-minute gap."] = "يجب ألا تتداخل الفترات، وأن يفصل بينها عشر دقائق على الأقل.",
        ["Day of week is required."] = "يجب تحديد اليوم.", ["Subject does not exist."] = "المادة غير موجودة.",
        ["Faculty member does not exist."] = "التدريسي غير موجود.", ["Classroom does not exist."] = "القاعة غير موجودة.",
        ["Time slot does not exist."] = "الفترة غير موجودة.", ["Section is required."] = "يجب تحديد الشعبة.",
        ["Section does not exist."] = "الشعبة غير موجودة.",
        ["The selected faculty member is not assigned to teach this subject."] = "التدريسي المحدد غير مكلّف بتدريس هذه المادة.",
        ["The selected subject does not have this lecture type."] = "المادة المحددة لا تتضمن هذا النوع من المحاضرات.",
        ["The selected classroom capacity is not enough for this section."] = "سعة القاعة المحددة لا تكفي للشعبة.",
        ["The selected section does not belong to the subject study year."] = "الشعبة المحددة لا تنتمي إلى مرحلة المادة.",
        ["The selected section is not valid for this subject."] = "الشعبة المحددة غير مناسبة للمادة.",
        ["Practical sessions must use A1/A2 for section A or B1/B2 for section B."] = "اختر A1 أو A2 لعملي الشعبة A، أو B1 أو B2 لعملي الشعبة B.",
        ["SemesterNumber must be 1 or 2."] = "الفصل الدراسي يجب أن يكون 1 أو 2.",
        ["FacultyMemberID or SubjectID does not exist in the database or preceding sheets."] = "معرف التدريسي أو المادة غير موجود في قاعدة البيانات أو الجداول السابقة.",
        ["StudyYearID does not exist."] = "معرف المرحلة غير موجود.", ["BranchID does not exist."] = "معرف الفرع غير موجود.",
        ["This study year requires general subjects and sections without a branch."] = "هذه المرحلة تتطلب مواد وشعباً عامة دون فرع.",
        ["This study year requires a BranchID."] = "يجب تحديد فرع لهذه المرحلة.",
        ["A record with the same name and scope already exists under a different ID. Use its existing ID."] = "يوجد سجل بالاسم والنطاق نفسيهما بمعرف آخر. استخدم معرفه الموجود.",
        ["A scheduled section cannot change its name, year or branch."] = "لا يمكن تغيير اسم أو مرحلة أو فرع شعبة لها محاضرات مجدولة.",
        ["New student count exceeds a booked room capacity."] = "عدد الطلبة الجديد يتجاوز سعة القاعة المحجوزة.",
        ["This room change would invalidate an existing lecture."] = "تعديل القاعة سيخالف شروط محاضرة مجدولة.",
        ["Import cannot change the year, branch or semester of a scheduled subject. Use the subject editor."] = "استخدم محرر المواد لتغيير مرحلة أو فرع أو فصل مادة مجدولة.",
        ["This change removes a lecture component already used in the timetable."] = "هذا التغيير يلغي جزءاً من مادة مستخدم في الجدول.",
        ["Teaching assignment reference no longer exists."] = "مرجع تكليف التدريس لم يعد موجوداً. أعد التحقق."
    };
    public static string Message(string message)
    {
        if (Messages.TryGetValue(message, out string? translated)) return translated;
        var rollback = Regex.Match(message, @"^(.+), row (\d+): (.+)\. The whole import was rolled back\.$");
        if (rollback.Success) return $"{ImportLabels.Display(rollback.Groups[1].Value)}، الصف {rollback.Groups[2].Value}: {Message(rollback.Groups[3].Value)} أُلغيت جميع تغييرات الاستيراد.";
        var excelRow = Regex.Match(message, @"^(.+), row (\d+), column (\d+): (.+)$");
        if (excelRow.Success) return $"{ImportLabels.Display(excelRow.Groups[1].Value)}، الصف {excelRow.Groups[2].Value}، العمود {excelRow.Groups[3].Value}: " +
            (excelRow.Groups[4].Value.StartsWith("replace formulas") ? "استبدل الصيغ بقيم ثابتة قبل الاستيراد." : "تحتوي الخلية قيمة خطأ في Excel.");
        var header = Regex.Match(message, @"^(.+): column (\d+) needs a text header in row 1\.$");
        if (header.Success) return $"{ImportLabels.Display(header.Groups[1].Value)}: يحتاج العمود {header.Groups[2].Value} إلى عنوان نصي في الصف الأول.";
        var duplicate = Regex.Match(message, @"^(.+): duplicate column (.+)\.$");
        if (duplicate.Success) return $"{ImportLabels.Display(duplicate.Groups[1].Value)}: عنوان العمود «{duplicate.Groups[2].Value}» مكرر.";
        if (message.EndsWith(": maximum 100 columns and 50,000 rows per sheet.")) return "الحد الأقصى لكل ورقة هو 100 عمود و50,000 صف.";
        if (message.StartsWith("Allowed section names: ")) return "أسماء الشعب المسموحة: " + message[23..];
        var length = Regex.Match(message, @"^(\w+) must be (\d+) characters or fewer\.$");
        if (length.Success) return $"{ImportLabels.Display(length.Groups[1].Value)}: الحد الأقصى {length.Groups[2].Value} حرفاً.";
        var assignment = Regex.Match(message, @"^Assign faculty to (.+) before hybrid generation\.$");
        if (assignment.Success) return "عيّن تدريسياً للمادة " + assignment.Groups[1].Value + " قبل التوليد الهجين.";
        var sections = Regex.Match(message, @"^No matching sections for (.+)\.$");
        if (sections.Success) return "لا توجد شعب مناسبة للمادة " + sections.Groups[1].Value + ".";
        if (message.Contains(": no placement found within the search limits;") || message.Contains(": no suitable classroom/laboratory"))
            return message.Replace(", section ", "، الشعبة ").Replace(", group ", "، المجموعة ").Replace("whole section", "الشعبة كاملة")
                .Replace(", Theory", "، نظري").Replace(", Practical", "، عملي")
                .Replace(": no placement found within the search limits; review locked bookings, teachers and periods.", ": لم يُعثر على موضع ضمن حدود البحث؛ راجع المحاضرات المثبتة والتدريسيين والفترات.")
                .Replace(": no suitable classroom/laboratory with sufficient capacity.", ": لا توجد قاعة أو مختبر مناسب بسعة كافية.");
        var field = Regex.Match(message, @"^(\w+): (.+)$");
        if (field.Success)
        {
            string detail = field.Groups[2].Value;
            string? local = detail switch
            {
                "value is required." => "القيمة مطلوبة.", "enter a whole number." => "أدخل عدداً صحيحاً.",
                "enter a finite nonnegative number." => "أدخل عدداً صالحاً غير سالب.",
                "use TRUE or FALSE." => "استخدم نعم أو لا.", "use HH:mm or an Excel time value." => "استخدم الوقت بالصيغة 08:30.",
                "invalid #ID reference." => "المعرف غير صالح. استخدم # متبوعاً برقم المعرف.", _ => null
            };
            if (detail.EndsWith("was not found in this scope.")) local = "لم يُعثر على المرجع " + detail[..detail.IndexOf(" was not found", StringComparison.Ordinal)] + " ضمن النطاق المحدد.";
            if (detail.EndsWith("is ambiguous. Use #ID or specify StudyYear/Branch.")) local = "الاسم ملتبس؛ استخدم # متبوعاً بمعرف السجل أو حدد المرحلة والفرع.";
            if (local is not null) return ImportLabels.Display(field.Groups[1].Value) + ": " + local;
        }
        var conflict = Regex.Match(message, @"^(Classroom|Faculty|Section/group) conflict with (.+) on (\w+), period #(\d+)\.$");
        if (conflict.Success)
            return $"تعارض في {ImportLabels.Display(conflict.Groups[1].Value)} مع {conflict.Groups[2].Value.Replace("saved lecture", "المحاضرة المحفوظة").Replace("row", "الصف")}، يوم {ImportLabels.Display(conflict.Groups[3].Value)}، الفترة #{conflict.Groups[4].Value}.";
        return message;
    }
}
