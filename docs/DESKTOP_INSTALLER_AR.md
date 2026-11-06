# نسخة تنصيب Windows مع البيانات الحالية

تُجهّز `tools/Build-DesktopInstaller.ps1` ملف Setup واحداً يتضمن:

- نسخة Windows Forms الحالية المنشورة بنواة x64، مع ملفات تشغيل .NET وCP-SAT وPDF.
- نسخة SQL كاملة من البيانات الحالية، تشمل الحسابات والمواد والتدريسيين والجداول.
- مثبّت Microsoft SQL Server 2025 LocalDB الرسمي، ليعمل التثبيت دون إنترنت.
- مثبّت Microsoft Visual C++ x64 الرسمي المطلوب للمكتبات الأصلية لمحرك الجدولة.

تستخدم النسخة المثبتة مثيل LocalDB مستقلاً باسم `UniversityTimetable`، وتضع ملفات
قاعدة البيانات في `%LOCALAPPDATA%\UniversityTimetable\Data`. لا تتصل نسخة التثبيت
بخادم `SQLEXPRESS` الأصلي، ولا تستبدل قاعدة محلية موجودة. يحتفظ إلغاء التثبيت
بملفات البيانات؛ إزالة البيانات يدوياً إجراء منفصل عن إزالة البرنامج.

التثبيت لكل مستخدم Windows على حدة. تثبيت محرك Microsoft عند غيابه يحتاج
صلاحيات مسؤول Windows، بينما تشغيل البرنامج واستخدام البيانات لا يحتاجانها.
تُستخدم حسابات تسجيل الدخول الموجودة في القاعدة المرفقة. ملف Setup يحتوي بيانات
المشروع الحالية؛ لا يُنشر علناً ضمن مصدر المشروع.

## إعادة بناء الحزمة

تحتاج عملية البناء إلى .NET SDK وInno Setup وملف `SqlLocalDB.msi` للإصدار 2025
و`vc_redist.x64.exe` من Microsoft. يُتحقق من توقيع حزم Microsoft قبل تضمينها.

```powershell
./tools/Build-DesktopInstaller.ps1 `
  -InnoCompiler 'C:\Tools\Inno Setup 6\ISCC.exe' `
  -LocalDbInstaller 'C:\Installers\SqlLocalDB.msi' `
  -VcRuntimeInstaller 'C:\Installers\vc_redist.x64.exe'
```

تُنشأ المخرجات في مجلد جديد داخل `output` ولا تستبدل مخرجات سابقة. تُنسخ البيانات
باستخدام SQL `BACKUP ... COPY_ONLY, CHECKSUM` ثم `RESTORE VERIFYONLY`؛ لا تُفصل
القاعدة الأصلية ولا تُعدّل سجلاتها. قد يختلف توقيت لقطة النسخ عن أعداد الصفوف
المقروءة إذا عُدّلت البيانات أثناء بناء الحزمة؛ اختبار المقارنة يوقف البناء عند ذلك.

قبل إنتاج Setup، يستعيد اختبار الحزمة البيانات في مثيل LocalDB تجريبي مستقل،
ويقارن أعداد صفوف جميع الجداول، ويؤكد عدم استبدال التعديلات عند إعادة التجهيز،
ويشغّل مسار تحديث القاعدة وCP-SAT من البرنامج المنشور. تُحذف القاعدة والمثيل
التجريبيان بعد الاختبار؛ لا يستخدم الاختبار القاعدة الأصلية.

يسجل المثبّت نتائج تجهيز القاعدة في
`%LOCALAPPDATA%\UniversityTimetable\setup-database.log`، وتتضمن المخرجات
`release-manifest.json` وبصمة SHA256 ونتائج التحقق.

مصادر الأدوات: [Microsoft LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)
و[Inno Setup](https://jrsoftware.org/isinfo.php).
