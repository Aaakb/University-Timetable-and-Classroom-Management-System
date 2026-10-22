# تنظيف التدريسيين — 18 أكتوبر 2026

طُبق طلب إبقاء تدريسيي الكورس الأول فقط على قاعدة UniversityTimetable في .\SQLEXPRESS.

- حُذف 17 سجلًا قديمًا خارج قائمة الكورس الأول؛ أصبح عدد التدريسيين 50.
- أُعيد تكليف 145 حصة للكورس الثاني بتدريسيين من الكورس الأول فقط، باستخدام 40 منهم.
- حُفظت 77 علاقة تكليف فعلية لمواد الكورس الثاني بدل العلاقات القديمة، مع تغطية جميع مواده الـ48.
- بقي جدول الكورس الأول النهائي وتكليفاته وأسماء التدريسيين وألقابهم كما هي.
- بقيت معرفات الحصص والأيام والأوقات والقاعات والمواد والشعب والمجموعات في الكورسين كما هي.
- لا يوجد أي تدريسي خارج قائمة الكورس الأول أو سجل غير مستخدم. لا توجد تعارضات في القاعات أو التدريسيين أو الطلاب.

## أساس التوزيع وحدوده

تكليفات الكورس الأول مأخوذة من الملف النهائي. تكليفات الكورس الثاني الجديدة تنظيمية بناءً على طلب المستخدم، وليست تكليفًا رسميًا مستخرجًا من ملف للكورس الثاني. اختيرت الأسماء من تدريسيي الكورس الأول فقط، مع تفضيل تدريس المادة نفسها أو مواد قريبة، ومراعاة التفرغ والتعارضات. تقارب أسماء المواد لا يثبت الاختصاص أو التكليف الرسمي، خصوصًا اللغة الإنجليزية والتشفير والتحقيق الجنائي.

## تكليفات الكورس الثاني بعد التنظيف

| المرحلة | الفرع | المادة | النظري | العملي |
|---|---|---|---|---|
| 1 | عام | Programming Fundamentals II | جلال حميد | حنان عبد الولي، روسم عبد العظيم |
| 1 | عام | Computer Skills II | الاء سنان | داليا شهاب |
| 1 | عام | Logic Design | منى جعفر | منى عبد الحسين |
| 1 | عام | Information Technology | شذى جاسم | — |
| 1 | عام | Mathematics | منى جعفر | — |
| 1 | عام | Democracy | علي سالم | — |
| 1 | عام | English Language | بتول زكي، سعد بادي | — |
| 2 | عام | Algorithm Design and Analysis | مها ادهم | مثنى صالح |
| 2 | عام | Database Systems | نرجس مزعل | مهند مؤيد جون |
| 2 | عام | Human Computer Interaction | الاء سنان | زياد فاروق |
| 2 | عام | Computer Architecture | منى عبد الحسين | اشراق خضير |
| 2 | عام | Computational Theory | منى جعفر | — |
| 2 | عام | Statistics and Probability | حازم مجمان | — |
| 3 | CS | Data Security | ورود حسن | انبثاق احمد |
| 3 | CS | Computer Networks | عباس عبد العزيز | مهند علي |
| 3 | CS | Image Processing | فرح قيس | فرح قيس |
| 3 | CS | Intelligent Systems | مهند تحرير | فراس علي |
| 3 | CS | Open Systems | عباس اكرم | رياض حازم |
| 3 | CS | English Language | سعد بادي | سعد بادي |
| 3 | Cyber | Stream and Block Ciphers | هدى عبد العالي | حازم مجمان |
| 3 | Cyber | Ethical Hacking | ياسمين حمزة | ياسمين حمزة |
| 3 | Cyber | Asymmetric Cryptography | حازم مجمان | ورود حسن |
| 3 | Cyber | Multimedia Security | جميلة حربي | جميلة حربي |
| 3 | Cyber | Computer Networks | عباس عبد العزيز | ايمن ماجد |
| 3 | Cyber | English Language | بتول زكي | — |
| 3 | IT | Computer Networks | عباس عبد العزيز | عباس عبد العزيز |
| 3 | IT | Database Query | نرجس مزعل | مهند مؤيد جون |
| 3 | IT | Information Technology and Security | ياسمين حمزة | انبثاق احمد |
| 3 | IT | Advanced Web Design and Programming | ايمان هاتو | انوار حسن |
| 3 | IT | Information Systems Maintenance | باسم جميل | باسم جميل |
| 3 | IT | English Language | علي سالم | بتول زكي |
| 4 | CS | Cloud Computing | ايثار عبد الوهاب | مهند علي |
| 4 | CS | Multimedia | جميلة حربي | ابراهيم نضير |
| 4 | CS | Intelligent Agent | مهند تحرير | فراس علي |
| 4 | CS | English Language | سعد بادي | — |
| 4 | CS | Research Project | امل حسين | — |
| 4 | Cyber | Information Hiding and Watermarking | محمد عزيز | محمد عزيز |
| 4 | Cyber | Database Security | ورود حسن | ورود حسن |
| 4 | Cyber | Digital Forensics | هدى عبد العالي | ايمن ماجد |
| 4 | Cyber | Operating Systems | عباس اكرم | رياض حازم |
| 4 | Cyber | English Language | سعد بادي | — |
| 4 | Cyber | Graduation Project | امل حسين | — |
| 4 | IT | Electronic Forensics | بشار مكي | هدى عبد العالي |
| 4 | IT | Cloud Computing | أسماء صادق | ايثار عبد الوهاب |
| 4 | IT | Multimedia | ابراهيم نضير | منى عبد الحسين |
| 4 | IT | Information Security | ياسمين حمزة | انبثاق احمد |
| 4 | IT | English Language | علي سالم | — |
| 4 | IT | Research Project | امل حسين | — |

## التحقق والنسخة الاحتياطية

نُفذ الاستيراد داخل معاملة واحدة مع رفض التنفيذ إذا تغيرت أي بيانات منذ لقطة المراجعة. جرى التحقق أولًا على نسخة مستقلة، ثم تأكد تطابق جميع جداول البيانات المعنية بين القاعدة الأصلية ونسخة المراجعة بعد التطبيق.

- PASS: all 132 lessons round-trip through ScheduleService validation without changes.
- PASS: first-year practical sessions still require an explicit group.
- PASS: whole-section practical uses all 30 seats, not half the section.
- PASS: whole-section practical blocks overlapping subgroup lessons.
- PASS: all 132 lessons appear in the display view with faculty names.
- PASS: all 145 retained semester-2 lessons satisfy the updated room and capacity rules.
- PASS: application startup preserves all 277 saved lessons.

النسخة الاحتياطية قبل التنظيف: `d:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\UniversityTimetable_before_faculty_cleanup_20261018.bak`، وقد اجتازت RESTORE VERIFYONLY WITH CHECKSUM.

الخطة التفصيلية بأسماء التدريسيين المحذوفين وكل استبدال محفوظة في `faculty-cleanup-plan.json`. ملفات التنفيذ والصورتان السابقة واللاحقة محفوظة في `.tmp/FacultyFinal2026`.
