# University Timetable Management System

تطبيق لإدارة الجداول الجامعية، مع إدارة القاعات والمختبرات والمواد والتدريسيين،
وواجهة Windows Forms وواجهة ويب قابلة للتثبيت كتطبيق PWA.

## البناء والتشغيل

لإنشاء نسخة مضغوطة صغيرة من الكود وملفات المشروع:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Export-Source.ps1
```

يُحفظ الملف داخل `output`، مع استبعاد `bin` و`obj` و`.git` و`.vs`
ومجلدات `out` و`output` و`tmp` و`.tmp` في جذر المشروع، والأرشيفات السابقة
والسجلات والملفات المؤقتة. لا يحذف الأمر أي ملفات أصلية.
هذه نسخة للمصدر تحتاج بناءً واستعادة حزم NuGet، وليست نسخة تشغيل جاهزة.

افتح `University Timetable Management System.slnx` في Visual Studio، أو نفّذ:

```powershell
dotnet build "University Timetable Management System.slnx"
dotnet run --project "University Timetable and Classroom Management System/Presentation_Layer.csproj"
```

لتشغيل الويب استخدم `Start-Web.cmd` أو `Start-Web.ps1`.
نسخة الويب تدعم استيراد Excel / PDF، وحفظ نسخ الجدول واستعادتها، وتثبيت المحاضرات وتفضيلات الأيام؛ راجع [تفاصيل استكمال وظائف الويب](docs/WEB_PARITY_AR.md).
لتعديل اتصال قاعدة البيانات في النسخة المثبتة، عدّل `database-settings.json` بجانب البرنامج؛ راجع [دليل إعداد الاتصال](docs/DATABASE_CONFIGURATION_AR.md).
التفاصيل في [دليل تشغيل الويب](docs/PWA_STARTUP_AR.md)،
و[دليل استيراد Excel](docs/ExcelImport.md)،
و[محرر أيام التدريسي](docs/FacultyScheduleEditor.md)،
و[تفضيلات التدريسيين بثلاث درجات وخطة حضور ثابتة](docs/FacultyPreferences.md)،
و[المقارنة بين جداول بديلة](docs/TimetableAlternatives.md)،
و[حفظ الجدول واستعادته](docs/TimetableBackups.md).

[التكليفات التدريسية](docs/TeachingAssignments.md) تحدد المرحلة والتخصص والمادة والشعبة،
ويلتزم التوليد بالشعبة المخصصة لكل تدريسي.

يمكن ضبط [عدد المحاضرات النظرية والعملية الأسبوعية](docs/SubjectLectureCounts.md)
تلقائياً أو تعديله يدوياً من صفحة المواد.

عملي [المرحلة الرابعة](docs/FourthYearPractical.md) للشعبة `A` كاملة في جميع التخصصات والفصلين، بدون `A1` و`A2`.

زر **توليد جدول جديد** يعيد بناء محاضرات الفصلين دون نسخة احتياطية تلقائية.
لحفظ الجدول السابق استخدم زر **حفظ نسخة الجدول**. التثبيت يدوي فقط بمربعات عمود **التثبيت**؛
كل المحاضرات الجديدة غير مثبتة افتراضياً، وإذا فشل التوليد يبقى الجدول السابق.

يفتح `Start-Desktop.cmd` نسخة `bin/Release/net8.0-windows` الافتراضية،
ويستخدم `out/subject-update` كبديل عند غيابها. أغلق النسخة المفتوحة قبل تحديث ملفاتها.

الاسم الرسمي هو **University Timetable Management System**. احتُفظ بمسارات
المشاريع الداخلية وNamespaces وأسماء Assemblies القائمة لحماية المراجع والتوافق.
تبقى قاعدة البيانات `UniversityTimetable` وإعدادات الاتصال القائمة كما هي.
