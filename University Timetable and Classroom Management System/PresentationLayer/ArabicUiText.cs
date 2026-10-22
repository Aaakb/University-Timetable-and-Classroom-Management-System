using System.Text.RegularExpressions;

namespace University_Timetable_and_Classroom_Management_System;

/// <summary>Localizes service diagnostics at the UI boundary.</summary>
internal static class ArabicUiText
{
    private static readonly Dictionary<string, string> Messages = new(StringComparer.Ordinal)
    {
        ["Enter the user name and password."] = "أدخل اسم المستخدم وكلمة المرور.",
        ["Invalid user name or password."] = "اسم المستخدم أو كلمة المرور غير صحيحة.",
        ["Signed in successfully."] = "تم تسجيل الدخول بنجاح.",
        ["This user name is already used."] = "اسم المستخدم مستخدم بالفعل.",
        ["Account created successfully."] = "تم إنشاء الحساب بنجاح.",
        ["Enter the full name."] = "أدخل الاسم الكامل.",
        ["Enter the user name."] = "أدخل اسم المستخدم.",
        ["Full name must be 150 characters or fewer, and user name 50 characters or fewer."] = "يجب ألا يتجاوز الاسم الكامل 150 حرفاً، واسم المستخدم 50 حرفاً.",
        ["User name must be at least 3 characters."] = "يجب ألا يقل اسم المستخدم عن 3 أحرف.",
        ["Password confirmation does not match."] = "تأكيد كلمة المرور غير مطابق.",
        ["Faculty member full name is required."] = "يجب إدخال الاسم الكامل للتدريسي.",
        ["Classroom capacity must be greater than zero."] = "يجب أن تكون سعة القاعة أكبر من صفر.",
        ["This faculty member is already assigned to the subject."] = "التدريسي مكلّف بتدريس هذه المادة بالفعل.",
        ["This teaching assignment is used by saved lessons. Reassign or remove those lessons before changing or deleting the assignment."] = "التكليف مستخدم في محاضرات محفوظة. أعد تكليفها أو احذفها قبل تعديل التكليف أو حذفه.",
        ["This change would make the classroom unsuitable for a saved lesson. Move those lessons before changing its type or reducing its capacity."] = "التعديل سيجعل القاعة غير مناسبة لمحاضرة محفوظة. انقل المحاضرات قبل تغيير نوع القاعة أو تقليل سعتها.",
        ["Remove this section's saved lessons before changing its study year, branch, or name."] = "احذف محاضرات الشعبة المحفوظة قبل تغيير مرحلتها أو فرعها أو اسمها.",
        ["The new student count exceeds the capacity of a room used by this section. Move those lessons before increasing the count."] = "عدد الطلبة الجديد يتجاوز سعة إحدى قاعات الشعبة. انقل المحاضرات قبل زيادة العدد.",
        ["Student count cannot be negative."] = "عدد الطلبة لا يمكن أن يكون سالباً.",
        ["First and second year sections must not be linked to a branch."] = "يجب ألا ترتبط شعب المرحلتين الأولى والثانية بفرع.",
        ["Third and fourth year sections must be linked to a branch."] = "يجب ربط شعب المرحلتين الثالثة والرابعة بفرع.",
        ["Section already exists for the same study year and branch."] = "توجد شعبة بالاسم نفسه في المرحلة والفرع المحددين.",
        ["Remove the linked lessons before removing this subject's theory or practical component."] = "احذف المحاضرات المرتبطة قبل إلغاء الجزء النظري أو العملي للمادة.",
        ["Remove this subject's scheduled lessons before changing its study year or branch."] = "احذف محاضرات المادة المجدولة قبل تغيير مرحلتها أو فرعها.",
        ["The semester change conflicts with an existing room, faculty, or section booking. The subject and its timetable have been kept unchanged."] = "تغيير الفصل يتعارض مع حجز قاعة أو تدريسي أو شعبة. احتُفظ بالمادة وجدولها دون تغيير.",
        ["This subject's lessons would conflict in the selected semester. The subject and its timetable have been kept unchanged."] = "محاضرات المادة ستتعارض في الفصل المحدد. احتُفظ بالمادة وجدولها دون تغيير.",
        ["Semester number must be 1 or 2."] = "يجب أن يكون الفصل الدراسي 1 أو 2.",
        ["Hours and credit units must be finite numbers that are zero or greater."] = "يجب أن تكون الساعات والوحدات أعداداً صالحة غير سالبة.",
        ["First and second year subjects must be general and not linked to a branch."] = "يجب أن تكون مواد المرحلتين الأولى والثانية عامة دون ارتباط بفرع.",
        ["Third and fourth year subjects must be linked to a branch."] = "يجب ربط مواد المرحلتين الثالثة والرابعة بفرع.",
        ["Subject already exists in the same study year, branch, and semester."] = "المادة موجودة بالفعل في المرحلة والفرع والفصل الدراسي المحددين.",
        ["This classroom is already booked in the selected semester, day, and time slot."] = "القاعة محجوزة بالفعل في الفصل واليوم والفترة المحددة.",
        ["This faculty member is already booked in the selected semester, day, and time slot."] = "التدريسي لديه محاضرة بالفعل في الفصل واليوم والفترة المحددة.",
        ["This section or group already has a schedule in the selected semester, day, and time slot."] = "الشعبة أو المجموعة لديها محاضرة بالفعل في الفصل واليوم والفترة المحددة.",
        ["A1, A2, B1, and B2 must be stored as practical groups, not as independent sections."] = "يجب حفظ A1 وA2 وB1 وB2 كمجموعات عملية ضمن الشعب.",
        ["Lecture type must be Theory or Practical."] = "يجب أن يكون نوع المحاضرة نظرياً أو عملياً.",
        ["Schedule day must be Sunday, Monday, Tuesday, Wednesday, or Thursday."] = "اختر يوماً دراسياً من الأحد إلى الخميس.",
        ["Add lecture periods in Time Slots before scheduling."] = "أضف فترات للمحاضرات في صفحة الفترات الدراسية قبل الجدولة.",
        ["Correct invalid lecture periods in Time Slots before scheduling."] = "صحح الفترات غير الصالحة في صفحة الفترات الدراسية قبل الجدولة.",
        ["Lecture periods must not overlap and must leave a 10-minute gap. Adjust Time Slots before scheduling."] = "يجب ألا تتداخل الفترات وأن يفصل بينها 10 دقائق. عدّل الفترات الدراسية قبل الجدولة.",
        ["Assign faculty members to subjects before generating. The existing timetable has been kept."] = "كلّف التدريسيين بالمواد قبل التوليد. احتُفظ بالجدول الحالي.",
        ["Add classrooms before generating. The existing timetable has been kept."] = "أضف قاعات قبل التوليد. احتُفظ بالجدول الحالي.",
        ["Assign teachers to all subjects before generating. The existing timetable has been kept."] = "كلّف تدريسيين بجميع المواد قبل التوليد. احتُفظ بالجدول الحالي.",
        ["Add matching sections for all subjects before generating. The existing timetable has been kept."] = "أضف شعباً مناسبة لجميع المواد قبل التوليد. احتُفظ بالجدول الحالي.",
        ["An existing lecture needs manual correction. The existing timetable has been kept."] = "توجد محاضرة تحتاج إلى تصحيح يدوي. احتُفظ بالجدول الحالي.",
        ["Locked lectures conflict. The existing timetable has been kept."] = "توجد محاضرات مثبتة متعارضة. احتُفظ بالجدول الحالي.",
        ["No lessons could be generated. Check subject hours, teaching assignments, sections, rooms, and time slots. The existing timetable has been kept."] = "تعذر توليد محاضرات. راجع ساعات المواد والتكليفات والشعب والقاعات والفترات. احتُفظ بالجدول الحالي.",
        ["A conflicting lecture could not be repaired. The existing timetable has been kept."] = "تعذر تصحيح محاضرة متعارضة. احتُفظ بالجدول الحالي."
    };

    private static readonly Dictionary<string, string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Branch"] = "الفرع", ["Classroom"] = "القاعة", ["Faculty member"] = "التدريسي",
        ["Faculty member subject assignment"] = "التكليف التدريسي", ["Schedule"] = "المحاضرة",
        ["Section"] = "الشعبة", ["Study year"] = "المرحلة الدراسية", ["Subject"] = "المادة", ["Time slot"] = "الفترة الدراسية",
        ["Branch name"] = "اسم الفرع", ["Classroom number"] = "رقم القاعة", ["Full name"] = "الاسم الكامل",
        ["Academic title"] = "اللقب العلمي", ["Section name"] = "اسم الشعبة", ["Study year name"] = "اسم المرحلة الدراسية",
        ["Subject name"] = "اسم المادة", ["Group name"] = "اسم المجموعة"
    };

    public static string Message(string message)
    {
        if (Messages.TryGetValue(message, out string? translated)) return translated;
        string imported = ImportUiText.Message(message);
        if (imported != message) return imported;

        var password = Regex.Match(message, @"^Password must be at least (\d+) characters\.$");
        if (password.Success) return $"يجب ألا تقل كلمة المرور عن {password.Groups[1].Value} أحرف.";

        var field = Regex.Match(message, @"^(.+?)( ID| name| number)? (not found|does not exist|is required|already exists)\.$");
        if (field.Success && Fields.TryGetValue(field.Groups[1].Value, out string? name))
        {
            string detail = field.Groups[2].Value switch { " ID" => "معرف ", " name" => "اسم ", " number" => "رقم ", _ => "" };
            return field.Groups[3].Value switch
            {
                "not found" or "does not exist" => $"لم يُعثر على {detail}{name}.",
                "is required" => $"يجب إدخال {detail}{name}.",
                _ => $"{detail}{name} موجود بالفعل."
            };
        }

        var length = Regex.Match(message, @"^(.+) must be (\d+) characters or fewer\.$");
        if (length.Success)
        {
            string fieldName = Fields.GetValueOrDefault(length.Groups[1].Value,
                BusinessLayer.ImportLabels.Display(length.Groups[1].Value));
            return $"يجب ألا يتجاوز {fieldName} {length.Groups[2].Value} حرفاً.";
        }

        const string practicalSubjects = "Assign teachers to all practical subjects before generating: ";
        if (message.StartsWith(practicalSubjects, StringComparison.Ordinal))
            return "كلّف تدريسيين بالمواد العملية الآتية قبل التوليد: " + message[practicalSubjects.Length..];
        const string practicalGroups = "Unable to place all practical groups. ";
        if (message.StartsWith(practicalGroups, StringComparison.Ordinal))
            return "تعذر جدولة جميع المجموعات العملية. " + Message(message[practicalGroups.Length..]);
        var missingSection = Regex.Match(message, @"^Add matching sections for practical subject (.+) before generating\. The existing timetable has been kept\.$");
        if (missingSection.Success)
            return $"أضف شعباً مناسبة للمادة العملية {missingSection.Groups[1].Value} قبل التوليد. احتُفظ بالجدول الحالي.";
        var unplaced = Regex.Match(message, @"^Unable to place (\d+) of (\d+) additional lessons without moving existing lectures\. (.*)$", RegexOptions.Singleline);
        if (unplaced.Success)
            return $"تعذر جدولة {unplaced.Groups[1].Value} من أصل {unplaced.Groups[2].Value} محاضرة إضافية دون نقل المحاضرات الحالية. " +
                Message(unplaced.Groups[3].Value);

        return message;
    }
}
