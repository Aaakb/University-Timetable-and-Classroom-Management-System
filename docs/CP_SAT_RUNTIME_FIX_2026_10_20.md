# إصلاح تحميل مكتبات CP-SAT على Windows — 20 أكتوبر 2026

## السبب المثبت

ظهرت رسالة `Google.OrTools.Sat.operations_research_satPINVOKE` قبل البحث عن أي جدول. كانت مكتبات OR-Tools موجودة، لكن استدعاء Windows لتحميلها من مجلد Release المعتاد أعاد الخطأ 206 (`The filename or extension is too long`). بلغ طول مسار المكتبة الرئيسية 259 حرفًا في موقع المشروع الحالي. نجح تحميل الملف نفسه عند استخدام صيغة المسار الممتد `\\?\` دون تغيير الملفات.

كانت اختبارات المحرر السابقة تعمل من مجلد اختبار أقصر، فلم تكشف مشكلة النشر في مسار المشروع الطويل.

## التعديل

- أضيف `FacultySchedulingRuntime` لتحميل مكتبة Windows x64 من مجلدها الكامل بصيغة مسار ممتد.
- تُحل اعتمادياتها من المجلد نفسه باستخدام `LoadLibraryExW` مع `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR` و`LOAD_LIBRARY_SEARCH_DEFAULT_DIRS`، دون تعديل PATH أو إعدادات Windows أو مسارات البحث العامة.
- يربط `NativeLibrary.SetDllImportResolver` مكتبة Google.OrTools بالمقبض المحمّل، ويُحتفظ به طوال عمر العملية. التهيئة متزامنة وتحدث مرة واحدة قبل أول بحث.
- يدعم موضع المكتبات في `runtimes/win-x64/native` وموضعها المسطح عند النشر بحسب RID. تظهر رسالة مفهومة عند غياب الملفات أو استخدام معمارية غير مدعومة.
- أضيف أمر `--check-cp-sat` إلى التطبيق لتشغيل مسألة صغيرة حقيقية والتحقق من مكتبات النشر **قبل** أي تهيئة واجهة أو اتصال بقاعدة البيانات.
- لم تُعدّل خوارزميات البحث أو تبادل العملي أو ملفات الاتصال أو بيانات الجدول.

## الملفات

- جديد: `Business_Logic_Layer/FacultyScheduling/FacultySchedulingRuntime.cs`.
- معدل: `Business_Logic_Layer/FacultyScheduling/FacultyCpSatPlanner.cs` لاستدعاء تهيئة المكتبة قبل البحث.
- معدل: `University Timetable and Classroom Management System/Program.cs` لإضافة فحص النشر دون قاعدة بيانات.
- جديد: `tests/NativeSchedulingRuntime/Verify-Deployment.ps1`.
- هذا التقرير.

## التحقق

- نجح Build Debug لكل مشاريع Solution دون أخطاء أو تحذيرات.
- نجح Build Release العادي بعد إغلاق التطبيق، وكذلك النسخة المستقلة في `output/cp-sat-runtime-release`، دون أخطاء أو تحذيرات.
- نجح فحص CP-SAT من ملف التطبيق في مجلد Release المعتاد ذي المسار الطويل، وأعاد `Optimal`.
- حل فعلي ناجح من التطبيق في مجلد Debug المعتاد.
- اختباران بإطلاق `Presentation_Layer.exe` الحقيقي: من مجلد التطبيق، ومن مجلد يزيد فيه مسار المكتبة الأصلية على MAX_PATH ويبلغ 280 حرفًا. كلاهما أعاد `Optimal` والخروج صفرًا.
- زُود فحص النشر عمدًا باتصال SQL غير صالح؛ نجح دون تهيئة أو استخدام قاعدة بيانات.
- اختبارات محرر أيام التدريسي: 22 ناجحة، بما فيها البحث الصارم والتفضيل والمعاينة والحفظ الآمن والإلغاء والواجهة العربية.
- النتائج في `output/native-runtime-verification/results.json` و`output/faculty-editor-verification/results.json`.

تشغيل فحص مجلد التطبيق:

```powershell
& "University Timetable and Classroom Management System/bin/Debug/net8.0-windows/Presentation_Layer.exe" --check-cp-sat
```

تشغيل اختبار المسار الطويل:

```powershell
& tests/NativeSchedulingRuntime/Verify-Deployment.ps1
```

## نسخة التشغيل

أُنشئت نسخة Release كاملة تتضمن الإصلاح وكل التعديلات السابقة في:

`output/cp-sat-runtime-release/bin/Presentation_Layer/release/Presentation_Layer.exe`

بعد إغلاق النسخة القديمة نجح تحديث مجلد التشغيل المعتاد أيضًا؛ يمكن تشغيل التطبيق بالطريقة المعتادة. يلزم إعادة التشغيل؛ فشل التهيئة في الجلسة القديمة لا يختفي بتكرار الضغط على التوليد. لا تُنهَ جلسة المستخدم تلقائيًا.

تستند آلية التحميل إلى [توثيق LoadLibraryExW من Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-loadlibraryexw) و[توثيق المسارات الطويلة](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).
