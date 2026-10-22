# University Timetable Management System

تطبيق لإدارة الجداول الجامعية، مع إدارة القاعات والمختبرات والمواد والتدريسيين،
وواجهة Windows Forms وواجهة ويب قابلة للتثبيت كتطبيق PWA.

## البناء والتشغيل

افتح `University Timetable Management System.slnx` في Visual Studio، أو نفّذ:

```powershell
dotnet build "University Timetable Management System.slnx"
dotnet run --project "University Timetable and Classroom Management System/Presentation_Layer.csproj"
```

لتشغيل الويب استخدم `Start-Web.cmd` أو `Start-Web.ps1`.
التفاصيل في [دليل تشغيل الويب](docs/PWA_STARTUP_AR.md)،
و[دليل استيراد Excel](docs/ExcelImport.md)،
و[محرر أيام التدريسي](docs/FacultyScheduleEditor.md)،
و[تفضيلات التدريسيين بثلاث درجات وخطة حضور ثابتة](docs/FacultyPreferences.md)،
و[المقارنة بين جداول بديلة](docs/TimetableAlternatives.md)،
و[حفظ الجدول واستعادته](docs/TimetableBackups.md).

يمكن ضبط [عدد المحاضرات النظرية والعملية الأسبوعية](docs/SubjectLectureCounts.md)
تلقائياً أو تعديله يدوياً من صفحة المواد.

زر **توليد جدول جديد** يعيد بناء محاضرات الفصلين، ويحفظ نسخة تلقائية من
الجدول السابق قبل استبداله. التثبيت يدوي فقط بمربعات عمود **التثبيت**؛
كل المحاضرات الجديدة غير مثبتة افتراضياً، وإذا فشل التوليد يبقى الجدول السابق.

إذا كانت النسخة المفتوحة تمنع استبدال ملفات البناء، أغلقها ثم استخدم
`Start-Desktop.cmd` لفتح النسخة المحدّثة من `out/subject-update`.

الاسم الرسمي هو **University Timetable Management System**. احتُفظ بمسارات
المشاريع الداخلية وNamespaces وأسماء Assemblies القائمة لحماية المراجع والتوافق.
تبقى قاعدة البيانات `UniversityTimetable` وإعدادات الاتصال القائمة كما هي.
