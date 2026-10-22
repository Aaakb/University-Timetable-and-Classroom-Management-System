# تقرير تدقيق طبقة الوصول إلى البيانات DAL

التاريخ: 23 أكتوبر 2026 — الحالة: التدقيق مكتمل، والدفعة A نُفذت بعد موافقة المستخدم؛ B–D لم تُنفذ.

تحديث لاحق بطلب مستقل من المستخدم: نُقل إعداد الاتصال من القيمة الثابتة إلى `database-settings.json` مع الحفاظ على override البيئي. تفاصيل التغيير ونتائج التحقق في `docs/DATABASE_CONFIGURATION_AR.md`. الأقسام التالية تسجل تدقيق النسخة السابقة وتنفيذ الدفعة A؛ ليست وصفًا محدثًا لمسار إعداد الاتصال بعد الطلب الجديد.

## النتيجة التنفيذية

DAL الحالية صغيرة: **10 ملفات C#، مجموعها 576 سطرًا، وملف مشروع واحد**. تحتوي على `AppDbContext` وتسعة أصناف ثابتة. لا تحتوي على Repositories أو Interfaces مخصصة أو Unit of Work أو مصانع إضافية يمكن التخلص منها. توجد 13 مجموعة `DbSet`، تشمل 12 كيان جدول وكيان عرض بلا مفتاح.

**لم يُثبت وجود صنف أو دالة عامة غير مستخدمة داخل DAL. لا أوصي بحذف أي منها.** كل المساعدات الحالية لها وظيفة فعلية ومستهلكون في المستودع. عدم وجود استدعاء صريح لخاصية تنقل أو إعداد EF ليس دليلًا على عدم الاستخدام؛ تدخل هذه الأجزاء في بناء النموذج، وتحميل العلاقات، والتسلسل، والتوافق مع قاعدة البيانات.

التنظيف الآمن المقترح محدود: إزالة مرجع NuGet مباشر مكرر، وتحسين أسماء الملفات ومواقعها، وتنسيق الكود المضغوط. التكرار الفعلي في كتابة الجداول موثق، لكن دمجه يتطلب تجريدًا إضافيًا أو تغيير المسؤوليات، لذلك لا يدخل في المقترح. قلة الحذف هنا نتيجة تتبع الاستخدام، وليست افتراضًا بأن الكود كله ضروري.

لم تتغير ملفات التطبيق أو الاختبارات أو إعدادات المشاريع خلال مرحلة التدقيق السابقة للموافقة. لم تُنشأ أو تُطبّق Migrations، ولم يُفتح اتصال بقاعدة البيانات، ولم تُنفّذ أوامر DDL أو CRUD. أضيف التقرير وملفات الأدلة، مع مخرجات البناء والاختبار المعتادة في المجلدات المولدة. بعد الموافقة تغير سطر واحد في ملف مشروع DAL كما هو موضح في سجل التنفيذ أدناه.

## النطاق والمنهج والأدلة

- قُرئت جميع ملفات DAL المصدرية، بما فيها كل الدوال والفروع وخصائص السياق وإعدادات الكيانات وملف المشروع.
- روجعت ملفات الكيانات الثلاثة عشر في `Models/Entities`، ومشاريع الحل الخمسة، ومراجع DAL في 29 مشروع اختبار وخمس أدوات مساعدة. فحص المراجع لا يعني تشغيل تلك المشاريع جميعًا.
- روجعت مسارات CRUD في خدمات BLL، والاستيراد، والاستعادة، والتفضيلات، والبدائل، وحفظ نواتج الجدولة، ومسارات تشغيل API وسطح المكتب.
- شمل البحث المراجع المباشرة و`Set<T>()` والانعكاس `GetMethod/GetProperty/GetProperties`، إضافة إلى استهلاك BLL من الواجهة وAPI. لا يُعد البحث النصي برهانًا على غياب مستهلك خارجي غير موجود في المستودع.
- يوجد ملفا حل `.slnx` بالمشاريع الخمسة نفسها؛ بُني `University Timetable Management System.slnx` كاملًا. مشاريع الاختبارات والأدوات ليست ضمن هذين الحلين.
- كانت شجرة العمل تحتوي على تعديلات سابقة في BLL والواجهة والاختبارات وملفات غير متتبعة، بينها أرشيفا ZIP. لم تُعدّل أو تُحذف هذه الأعمال. التدقيق مبني على نسخة العمل الحالية، وليس HEAD وحده. لم تُعامل الأرشيفات كنسخ مصدرية مبنية أو كمرشحات حذف.

الأدلة المحفوظة في `docs/Verification/DalAudit/`:

| الملف | المحتوى |
|---|---|
| `dal-file-inventory.json` | جرد ملفات DAL، عدد الأسطر والحجم وبصمة SHA256 |
| `dal-consumers.txt` | مواضع التصريح والاستهلاك بالأسماء وأرقام الأسطر |
| `entity-consumers.txt` | مراجع مجموعات الكيانات، والاستخدام العام والانعكاس |
| `query-transaction-evidence.txt` | التحميل والتتبع والحفظ والمعاملات والعمليات الجماعية |
| `working-tree-before-report.txt` | حالة العمل السابقة لكتابة التقرير، بعد إنشاء مجلد الأدلة |
| `baseline-checks.json` | النتائج التفصيلية للفحوص الـ51 المنفذة |
| `baseline-verification.txt` | أوامر التحقق ونتائجها وحدودها |

المسارات وأرقام الأسطر أدناه تخص نسخة العمل وقت التدقيق. التصنيفات: **1 مؤكد عدم الاستخدام؛ 2 مكرر/زائد لكنه مستخدم؛ 3 يحتمل عدم الحاجة؛ 4 ضروري**. خطورة الحذف منفصلة عن خطورة التعديل المقترح.

## جرد جميع مكونات DAL وقرارات الاحتفاظ

| المكوّن/العقد | التصنيف | دليل الاستهلاك والسبب | خطورة الحذف |
|---|---|---|---|
| `DatabaseConnection.cs:5` — `EnvironmentVariableName` | 4 | `UniversityTimetable.API/Program.cs:15`، أدوات الاستيراد والاستعادة واختبارات SQL تضبط المفتاح نفسه | عالية |
| `DatabaseConnection.cs:7` — `ConnectionString` | 4 | `DbContext/appDbContext.cs:35`، وأدوات مثل `tools/ImportFacultyDays/Program.cs:15`؛ يجمع override البيئي والافتراضي المحلي | عالية |
| `DbContext/appDbContext.cs:8` — المنشئ الافتراضي | 4 | خدمات CRUD و`UniversityTimetable.API/Program.cs:91` تنشئ السياق مباشرة | عالية |
| `DbContext/appDbContext.cs:12` — منشئ `DbContextOptions` | 4 | اختبارات مثل `tests/ExcelImport/Program.cs:104` و`:105` تستخدم options وinterceptors، وخدمات تقبل `Func<AppDbContext>` | عالية |
| `DbContext/appDbContext.cs:31` — `OnConfiguring` | 4 | يوفّر SQL Server عند عدم وجود إعداد؛ شرط `IsConfigured` يحفظ إعدادات السياق المحقونة للاختبار | عالية |
| `DbContext/appDbContext.cs:39` — `OnModelCreating` | 4 | EF يبني النموذج منه ضمنيًا؛ يحدد المفاتيح والعلاقات والفهارس والعرض والتزامن | حرجة |
| `Importing/ImportTransaction.cs:9` — `ExecuteAsync<T>` | 4 | `Business_Logic_Layer/Importing/ExcelImportService.cs:342`؛ سياق ومعاملة Serializable للعملية كلها، وفحص الإلغاء قبل commit | عالية |
| `Importing/ScheduleLockSchema.cs:7` — `EnsureAsync` | 4 | `Business_Logic_Layer/Maintenance/DatabaseMaintenanceService.cs:24`؛ توافق عمود تثبيت المحاضرات مع القواعد القديمة، واختبارات التكرار في `tests/ExcelImport/Program.cs:124` | عالية |
| `Scheduling/SubjectDeletionSchema.cs:7` — `EnsureAsync` | 4 | `DatabaseMaintenanceService.cs:20`؛ يضيف الحذف المنطقي ويحوّل فهرس المواد إلى فهرس مفلتر | حرجة |
| `Scheduling/SubjectLectureCountSchema.cs:7` — `EnsureAsync` | 4 | `DatabaseMaintenanceService.cs:21`؛ يضيف العدّين الاختياريين مع الاحتفاظ بمعنى NULL | عالية |
| `Scheduling/FacultyAssignmentScopeSchema.cs:7` — `EnsureAsync` | 4 | `DatabaseMaintenanceService.cs:22` و`tools/AssignmentScopeUpgrade/Program.cs:22`؛ أعمدة النطاق والنوع وFK وCHECK وفهرس الشعبة | حرجة |
| `Scheduling/FacultyPreferenceSchema.cs:7` — `EnsureAsync` | 4 | `DatabaseMaintenanceService.cs:25` و`UniversityTimetable.API/Program.cs:92`؛ إنشاء جدولَي التفضيلات للقواعد الموجودة | حرجة |
| `Scheduling/FacultyScheduleWriter.cs:11` — `ReplacePlacementsAsync` | 4؛ جزء داخلي مكرر من التصنيف 2 | المحرر `FacultyScheduleEditorService.cs:154`، الأيام `FacultyDayPreferenceService.cs:50`، البدائل `TimetableAlternativesService.cs:77`، إصلاح الاستيراد `ImportedTimetableRepair.cs:147`، وثلاث أدوات؛ يسمح بالتبديل الدوري تحت الفهارس الفريدة | حرجة |
| `Scheduling/TimetableSnapshotStore.cs:8` — `ReplaceAsync` | 4؛ جزء داخلي مكرر من التصنيف 2 | `Business_Logic_Layer/Backups/TimetableBackupService.cs:62`؛ استبدال جميع المحاضرات مع حفظ معرفاتها داخل معاملة المستدعي | حرجة |

جميع فروع DAL لها معنى: اشتراط المعاملة، القوائم الفارغة، اختلاف مزود البيانات، وجود أعمدة identity، نجاح/فشل حفظ البيانات، وإطفاء `IDENTITY_INSERT` عند الفشل. لم يثبت وجود فرع غير قابل للوصول. كتل `catch` الداخلية تحافظ على الخطأ الأصلي عند فشل التنظيف؛ لا يجوز حذفها بوصفها ابتلاعًا عشوائيًا للأخطاء.

تعريفات `DbSet` وخصائصها العامة ليست مرشحة للحذف أو تحويل getters/setters في هذا العمل. كما أن `IsRequired` و`HasKey` و`ValueGeneratedNever` حتى عندما تتفق مع conventions توثق عقد النموذج؛ حذفها لا يحقق تبسيطًا يستحق المخاطرة.

## مجموعات DbSet والكيانات والعلاقات

| DbSet / الكيان | المفتاح | المستهلكون والوظيفة | العلاقات وسلوك الحذف الواجب حفظه |
|---|---|---|---|
| `Branches` / `Branch` | `BranchID` يدوي | `BranchService`، الاستيراد، تحميل هيكل الجدولة | طرف أصلي للمواد والشعب والمحاضرات؛ العلاقات التابعة Restrict |
| `StudyYears` / `StudyYear` | `StudyYearID` يدوي | `StudyYearService`، الاستيراد والجدولة | طرف أصلي للمواد والشعب والمحاضرات؛ Restrict |
| `Sections` / `Section` | `SectionID` يدوي | `SectionService`، التكاليف والجدولة | مرحلة مطلوبة وفرع اختياري، كلاهما Restrict؛ المحاضرات والتكاليف تشير إلى الشعبة |
| `Subjects` / `Subject` | `SubjectID` يدوي | `SubjectService`، التوليد، الاستيراد والاستعادة | المرحلة مطلوبة والفرع اختياري Restrict؛ التكاليف Cascade والمحاضرات Restrict |
| `FacultyMembers` / `FacultyMember` | `FacultyMemberID` يدوي | خدمة التدريسيين والتكاليف والتفضيلات والجدولة | التكاليف والتفضيلات Cascade؛ المحاضرات Restrict |
| `FacultyMemberSubjects` / `FacultyMemberSubject` | `(FacultyMemberID, SubjectID)` | خدمة التكاليف، `AcademicExcelImport.SaveAsync`، التوليد والتحقق | التدريسي والمادة Cascade؛ الشعبة الاختيارية Restrict؛ `LectureType` له معنى عمل |
| `Classrooms` / `Classroom` | `ClassroomID` يدوي | `ClassroomService`، فحص السعة واختيار القاعة | المحاضرات Restrict |
| `TimeSlots` / `TimeSlot` | `TimeSlotID` يدوي | `TimeSlotService`، فحوص التعارض والجدولة والتفضيلات | المحاضرات وتفضيلات الفترات Restrict |
| `Schedules` / `Schedule` | `ScheduleID`؛ لا يُضبط `ValueGeneratedNever` | `ScheduleService`، المحرر، البدائل، الاستيراد والنسخ | سبع علاقات Restrict: المادة، التدريسي، القاعة، الفترة، المرحلة، الفرع، الشعبة؛ الثلاث الأخيرة nullable في النموذج |
| `ScheduleDetails` / `ScheduleDetailsView` | بلا مفتاح | `ScheduleService.cs:37` ثم شاشة الجداول و`SchedulingController` | `ToView("vw_ScheduleDetails")`؛ للقراءة وليس جدول CRUD أو نسخة زائدة من `Schedule` |
| `ApplicationUsers` / `ApplicationUser` | `ApplicationUserID` يدوي | `AuthService` و`AuthController` | لا علاقات معرفة؛ username الموحّد فريد |
| `FacultySchedulingPreferences` | `(FacultyMemberID, SemesterNumber)` | `FacultyPreferenceService`، التوليد، `ImportCatalog` | التدريسي Cascade؛ `Revision` concurrency token وليس حقلًا زائدًا |
| `FacultyPeriodPreferences` | `(FacultyMemberID, SemesterNumber, DayOfWeek, TimeSlotID)` | خدمة التفضيلات وكتالوج الجدولة | الملف الأب Cascade؛ الفترة Restrict؛ CHECK لليوم والمستوى |

خصائص التنقل ومجموعات العلاقات في `Models/Entities/*.cs` تدخل في Fluent API و`Include/ThenInclude` وقراءة الواجهة. بعضها يُستخدم ضمنيًا حتى عند غياب وصول مباشر. لا أوصي بحذف أي كيان أو علاقة أو خاصية persistable.

خصائص `Subject` المحسوبة `[NotMapped]` ليست أعمدة زائدة: تعطي القيم الافتراضية والفعالة لعدد المحاضرات، ويستخدم التوليد `HasWeeklyLectures`. العدّان nullable يميزان الوضع التلقائي عن الصفر الصريح. `RequirementType` محفوظ لتوافق القاعدة والبيانات التاريخية؛ `SubjectService.UpdateInContextAsync` يحتفظ بقيمته. `IsDeleted` يحفظ المحاضرات والتكاليف القديمة، فلا يستبدل بفلتر عام يُخفيها عن الاستعادة. `IsLocked` جزء من قواعد الحفظ والتوليد والاستيراد والنسخ.

## الفهارس والقيود

الاحتفاظ بجميع تعريفات المفاتيح الأساسية والأجنبية والفهارس الحالية إلزامي. الاستخدام ليس مرهونًا بظهور اسم الفهرس في C#.

| الموقع في `DbContext/appDbContext.cs` | التعريف |
|---|---|
| 69، 83، 166، 203، 316 | فهارس فريدة لأسماء الفروع والمراحل والتدريسيين وأرقام القاعات واسم المستخدم الموحّد |
| 97 | فريد `(StudyYearID, BranchID, SectionName)` |
| 124 | فريد `(SubjectName, StudyYearID, BranchID, SemesterNumber)` مع `[IsDeleted] = 0` |
| 223 | فريد `(StartTime, EndTime, IsBreak)` |
| 243 | `UQ_Classroom_Semester_Time`: القاعة والفصل واليوم والفترة |
| 247 | `UQ_Faculty_Semester_Time`: التدريسي والفصل واليوم والفترة |
| 251 | `UQ_Section_Semester_Time`: الشعبة والفصل واليوم والفترة والمجموعة |
| 46، 47 | CHECK لمستوى تفضيل الفترة واليوم |
| 60 | concurrency token على `Revision` |

توجد أيضًا قيود داخل SQL توافق الإصدارات، منها CHECK للفصل في `FacultyPreferenceSchema.cs`، وCHECK لنوع التكليف في `FacultyAssignmentScopeSchema.cs`، وdefault constraints وفهرس FK الشعبة. هذه ليست مهملة لأنها خارج Fluent API. فهارس FK التي يبنيها EF بالـconventions جزء من النموذج أيضًا، ولا تدخل في أي حذف مقترح.

## التتبع عبر الطبقات وملكية السياق والمعاملة

1. سطح المكتب يستدعي خدمات BLL من الشاشات؛ لم يُظهر البحث وصولًا مباشرًا إلى `AppDbContext` في ملفات C# الخاصة بالواجهة. تشغيله يمر عبر `DatabaseMaintenanceService.ApplyPendingFixesAsync` قبل شاشة الدخول.
2. API يستخدم BLL في Controllers وفي `ResourceCatalog`، لكنه يصل أيضًا مباشرة إلى السياق في `AuthController.cs:24` و`OverviewController.cs:15` و`SchedulingController.cs:56`، ويستدعي schema helper عند التشغيل. لذلك DAL ليست مستهلكة من BLL وحده.
3. `UniversityTimetable.API/Program.cs:44` وما بعدها تسجل خدمات BLL بـScoped، و`ResourceCatalog` بـSingleton. لا يوجد `AddDbContext` أو DbContext Singleton؛ الخدمات تملك سياقات قصيرة العمر لكل عملية. عدم وجود تسجيل للسياق ليس تسجيلًا ناقصًا في هذه البنية.
4. `Func<AppDbContext>` في خدمات التفضيلات والمحرر والبدائل والنسخ والاستيراد يُستخدم لإنشاء سياقات قابلة للاختبار. حذف منشئ options أو هذه العقود يكسر اختبارات interceptors واختيار قواعد الاختبار.
5. `ImportCatalog.LoadAsync` يستخدم `db.Set<T>()` لقراءة 11 نوعًا من الكيانات دون تتبع، ثم يحسب بصمة بيانات. يقرأ scalar properties بالانعكاس أيضًا. لذلك لا يجوز استنتاج أن كيانًا أو خاصية غير مستخدمة بالاعتماد على أسماء DbSet وحدها.
6. خوارزميات التخطيط تستقبل بيانات محملة في الذاكرة؛ الاتصال والتحميل والحفظ في خدماتها المحيطة. `ScheduleConflictChecker` ينفذ `AnyAsync` على القاعدة وله فحص أزواج داخل الذاكرة؛ هذان ليسا تكرارًا يمكن استبدال أحدهما بالآخر دون تغيير توقيت التحقق ودلالاته.

| مسار العملية | الملكية والدلالة الحالية الواجب حفظها |
|---|---|
| `ImportTransaction.ExecuteAsync` | يملك السياق ومعاملة Serializable، يمرر السياق نفسه إلى العمل، يفحص الإلغاء قبل commit؛ disposal يلغي المعاملة عند الفشل |
| استيراد Excel | عدة `SaveChangesAsync` ضمن معاملة واحدة؛ الحفظ المتدرج يجعل الصفوف السابقة مرئية لفحص التعارض. لا يُستبدل تلقائيًا بحفظ واحد |
| `FacultyScheduleWriter.ReplacePlacementsAsync` | يشترط معاملة من المستدعي، يحذف المعرفات المتغيرة فقط، يتحقق من عدد المحذوف، يعيد إدراج المعرفات نفسها، ولا ينفذ commit |
| `TimetableSnapshotStore.ReplaceAsync` | يشترط معاملة من المستدعي، يحذف جميع المحاضرات حتى عندما تكون النسخة فارغة، ويعيد الإدراج عند وجود صفوف؛ لا ينفذ commit |
| المحرر والبدائل وتحسين الأيام | المستدعي يملك Serializable والتحقق من البصمة والقيود ومطابقة النتيجة، ثم commit؛ تحسين الأيام يحفظ التفضيلات وحركة المحاضرات معًا |
| استعادة النسخة | Serializable؛ فحص المراجع والنسخة، كتابة نسخة أمان قبل الحذف، استبدال جميع الصفوف، فحص البصمة ثم commit |
| توليد/إعادة توليد الجدول | Serializable؛ تحميل البيانات والقيود، الاحتفاظ بالأقفال، نسخة أمان قبل الحذف، حفظ التغييرات ثم commit؛ لا تغيير في الخوارزمية |
| تحديث المادة/الشعبة/القاعة/الفترة والتكليف | معاملات صريحة في العمليات التي تحمي محاضرات محفوظة؛ دوال `*InContextAsync` تسمح للاستيراد بإعادة استخدام المعاملة |
| `ManualKeySaveHelper` خارج DAL | يميز identity عن المفتاح اليدوي، يعيد استخدام معاملة المستدعي أو ينشئ معاملة عند الحاجة؛ ليس بديلًا عامًا لـSaveChanges |

قراءات القوائم والعلاقات تعتمد غالبًا على `AsNoTracking` و`Include`. عمليات التعديل تستخدم كيانات متتبعة و`CurrentValues.SetValues` أو `Update` أو `FindAsync`. في التوليد، تحميل `existingSchedules` متتبع ومقصود للحذف والتعديل اللاحق؛ إضافة NoTracking عامة تغيّر السلوك.

`ExecuteDeleteAsync` و`ExecuteUpdateAsync` عمليتان مباشرتان تتجاوزان ChangeTracker؛ الكتالوج عادة محمل دون تتبع، لكن تمرير سياق يملك محاضرات متتبعة إلى writers قد يسبب تعارض تتبع. هذا قيد على المستدعي يحتاج اختبارًا، وليس سببًا لإضافة `ChangeTracker.Clear()` الذي قد يمس تعديلات التفضيلات في المعاملة نفسها.

التصفية في `ScheduleService.GetScheduleDetailsByFiltersAsync` تتم قبل `ToListAsync`، ثم ترتيب أكاديمي ويومي في الذاكرة. الاستعلام الفرعي إلى `Schedules` يظل استعلامًا قبل materialization، وليس تحميلًا ثانيًا مثبتًا لكل الصفوف. لا أعدّه كودًا ميتًا أو زائدًا دون قياس SQL والخطة.

## النتائج المصنفة والتكرارات

| الرمز | التصنيف | الدليل والاستنتاج | القرار والمخاطرة |
|---|---|---|---|
| F01 | 1: مؤكد عدم الاستخدام | لا صنف أو دالة أو واجهة أو DbSet داخل DAL ثبت عدم استخدامها | لا حذف مصدر عام؛ لا يوجد عدد حذف مصطنع |
| F02 | 2: زائد لكنه مستخدم | `Data_Access_Layer.csproj` يذكر Core وSqlServer بالإصدار `8.0.28`. `obj/project.assets.json` يثبت SqlServer → Relational → Core بالإصدار نفسه | يمكن إزالة **مرجع Core المباشر فقط**؛ EF Core نفسه ضروري ويبقى transitive. مخاطرة منخفضة مع مقارنة graph والبناء |
| F03 | 2: تكرار مستخدم | `FacultyScheduleWriter.cs:19` وما بعده و`TimetableSnapshotStore.cs:13` وما بعده يكرران اكتشاف identity وON/OFF والإدراج ومعالجة الفشل | الاحتفاظ حاليًا؛ توحيدهما مخاطرة عالية ويفرض helper/مسؤولية جديدة غير مطلوبة |
| F04 | 2: تشابه ضروري | `ManualKeySaveHelper.cs` خارج DAL ينفذ بعض خطوات identity نفسها، لكنه يختلف في الجداول المسموحة وملكية المعاملة وحفظ التغييرات | الاحتفاظ؛ ليس نسخة مكافئة من writers |
| F05 | 2: تداخل مستخدم | Fluent API وSQL الصيانة يصفان بعض الجداول والقيود نفسها؛ أحدهما نموذج EF والآخر ترقية قواعد موجودة | الاحتفاظ؛ إزالة SQL تكسر القواعد القديمة، وإزالة النموذج تغيّر EF |
| F06 | 2: تكرار فعلي خارج DAL | `tools/AdoptPdfAssignments/Program.cs:33` و`:34` يستدعيان `FacultyAssignmentScopeSchema.EnsureAsync(db)` مرتين متتاليتين | لا يُحذف helper. حذف الاستدعاء الثاني منخفض المخاطرة لكنه خارج نطاق DAL المقترح |
| F07 | 2: تكرار SQL خارج DAL | `DatabaseMaintenanceService.NormalizeScheduleDataAsync` يصفّر GroupName للنظري عند السطرين 149 و181، وبينهما تحويل أقسام عملية قد يغيّر LectureType | الاحتفاظ في هذا التدقيق؛ عدم تبادل موضعي الأمرين أو حذف أحدهما دون اختبار بيانات قديمة |
| F08 | 3: احتمال عدم الحاجة في بعض البيئات | schema helpers قد تكون no-op على قاعدة حديثة، لكن تشغيل سطح المكتب وAPI والأدوات ما زال يستدعيها | لا دليل أن جميع قواعد النشر تمت ترقيتها؛ لا حذف |
| F09 | 4: ضروري | `ImportTransaction` ليس wrapper لـSaveChanges فقط؛ يحدد العزل وملكية السياق والإلغاء والذرية | الاحتفاظ |
| F10 | 4: ضروري | دوال BLL `AddInContextAsync/UpdateInContextAsync` مستخدمة في `AcademicExcelImport.SaveAsync` لحفظ العملية كاملة بنفس السياق | لا حذف أو دمج في public wrappers تنشئ سياقات مستقلة |

**قائمة المكونات المؤكدة غير الضرورية:** لا مكونات C# مثبتة للحذف. المرجع المباشر الثاني إلى EF Core مكرر في graph الحالي، وليس حزمة EF Core غير مستخدمة. لا توجد حزم DAL أخرى غير هاتين الحزمتين، ومرجع `Models` مطلوب. لا تُحذف `bin/obj` أو أرشيفات المستخدم كجزء من تنظيف المصدر.

لم تظهر Repositories قديمة أو Interfaces مهجورة أو إعدادات migrations أو DI زائدة داخل DAL. لا أوصي بإضافة أي منها أو نقل منطق BLL إلى خدمات جديدة.

## مسائل تحتاج مراجعة منفصلة، ولا تدخل في التنظيف

| الرمز | الموقع والدليل | الاستنتاج وحدوده | الخطورة |
|---|---|---|---|
| R01 | `FacultyPreferenceSchema.cs` يضيف `CK_FacultySchedulingPreferences_Semester`؛ إعداد `FacultySchedulingPreference` في السياق لا يعرّفه. `FacultyAssignmentScopeSchema.cs` يضيف CHECK لنوع التكليف بينما Fluent API لا يعرّفه | اختلاف مؤكد بين مصدري تعريف المخطط. قاعدة منشأة بـEnsureCreated وحده قد تختلف عن قاعدة مرت بالصيانة. لا نضيف قيدًا أو migration في هذا العمل | عالية |
| R02 | `DatabaseMaintenanceService.cs:261` و`:270` و`:279` لا تنشئ الفهارس الفريدة عند وجود تكرارات؛ بعض الفهارس القديمة تُزال قبل ذلك | احتمال غياب حماية قاعدة البيانات بعد صيانة بيانات متعارضة. يحتاج قراءة `sys.indexes/sys.key_constraints` الفعلية؛ لم تفحص القاعدة | عالية |
| R03 | `FacultyScheduleEditorService.cs:150` يستدعي `ValidatePlanAsync` دون تمرير سياق الحفظ؛ الدالة عند `:168` تنشئ سياقًا آخر. `TimetableAlternativesService.ValidateAsync` ينشئ سياقًا منفصلًا أيضًا | التحقق ليس كله داخل معاملة الحفظ نفسها. قد تظهر اختلافات اتساق أو حجب تحت التزامن. هذا خطر محتمل، وليس فشلًا مثبتًا. تمرير سياق واحد يغيّر دلالة المعاملة ويتطلب موافقة مستقلة | عالية |
| R04 | `AutoKeyGenerator.NextAsync` و`FirstAvailableAsync` ثم الحفظ في خدمات مثل Branch/Auth، دون معاملة Serializable تحيط بتخصيص المفتاح كله | احتمال اصطدام معرفات بين طلبات متزامنة. المفاتيح والقيود تمنع بعض النتائج، لكنها لا تضمن نجاح كلا الطلبين؛ لا تغيير لاستراتيجية المفاتيح | متوسطة |
| R05 | `DatabaseMaintenanceService.ApplyPendingFixesAsync` و`UniversityTimetable.API/Program.cs:92` | تشغيل التطبيق ليس اختبارًا للقراءة فقط: يشغّل DDL، وصيانة سطح المكتب تغيّر بيانات وفهارس وعرضًا. لهذا لم تُشغّل التطبيقات أو اختبارات التكامل | عالية |
| R06 | `FacultyScheduleWriter.cs:14` و`TimetableSnapshotStore.cs:10` | يتحققان من وجود معاملة، لا من أن عزلها Serializable. مستهلكو BLL المراجعون يحددونه؛ العقد العام أوسع من التعليق. لا تضف تحققًا جديدًا في تنظيف يحفظ السلوك | متوسطة |
| R07 | SQL Server unique indexes مع أعمدة nullable في Sections وSchedules، وتعريفات SQL اليدوية للفهرس | يلزم مقارنة filters والقيود الفعلية ونموذج EF، بما في ذلك ما تضيفه conventions؛ لم يُستخرج المخطط أو النموذج وقت التشغيل، فلا ندعي تطابقهما الكامل | عالية |
| R08 | بعض حدود BLL أقل من المخطط: YearName 50 مقابل 100، SubjectName 100 مقابل 150، اسم التدريسي 100 مقابل 150، رقم القاعة 20 مقابل 100 | فروق مؤكدة في المصدر، لكنها قيود سلوك حالية. توحيدها يوسع أو يضيّق المدخلات؛ خارج النطاق | منخفضة/متوسطة |
| R09 | `ImportCatalog.LoadAsync` يحمّل جداول كاملة ويحسب بصمتها؛ `ScheduleService` يحمّل موارد الفصل والتفضيلات للتحقق | تكلفة محتملة عند زيادة البيانات، لكنها تحقق الاتساق والتخطيط. لا تُختزل الاستعلامات دون قياس وتحقق من معنى البصمة | متوسطة |
| R10 | مسار `DatabaseConnection` يستخدم البيئة، وAPI يحوّل إعداد connection string إلى متغير البيئة عند التشغيل | آلية قائمة مشتركة مع الأدوات والاختبارات؛ لا توحيد جديد للإعداد ولا إضافة factory/DI تلقائيًا. فراغ القيمة البيئية يختلف عن غيابها، وتغييره ليس تنظيفًا محضًا | متوسطة |

لم توجد ملفات migrations أو model snapshot في المصدر المفحوص. التوافق الحالي يعتمد على Fluent model وEnsureCreated ومساعدات SQL وصيانة BLL. غياب migrations لا يبرر حذف التوافق القديم، ولا يعني أننا تحققنا من مخطط كل قاعدة منشورة.

## التغييرات المقترحة للموافقة

هذه قائمة المقترحات الأصلية للمراجعة؛ نُفذت A وحدها لاحقًا وفق السجل أدناه:

| الدفعة | الملفات | التغيير | ما يثبت قبوله | الخطر |
|---|---|---|---|---|
| A | `Data_Access_Layer/Data_Access_Layer.csproj` | إزالة `<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.28" />` فقط، مع إبقاء SqlServer بنفس الإصدار | restore ومقارنة versions/assets المتاحة لـDAL ومستهلكيها، بناء الحل، الفحوص المتاحة؛ التراجع عن الحذف إن اختلفت أصول لازمة | منخفض |
| B — اختيارية | `Data_Access_Layer/DbContext/appDbContext.cs` | إعادة تسمية الملف إلى `AppDbContext.cs` فقط، دون اسم صنف/namespace أو API جديد؛ نقل مرحلي على Windows لحفظ تغيير حالة الأحرف في Git | مقارنة المحتوى والبصمة، البحث عن أي مرجع مسار خاص، build | منخفض جدًا |
| C — اختيارية | مساعدات schema الخمسة | نقل `ScheduleLockSchema` من Importing والأربعة من Scheduling إلى `Data_Access_Layer/Schema/`؛ إبقاء namespace والأصناف والدوال وSQL كما هي | خمس عمليات نقل فقط، مقارنة البصمات، لا تغيير model أو SQL، بناء الحل | منخفض؛ تحقّق من أي أدوات خارجية تعتمد المسارات |
| D — اختيارية | `Data_Access_Layer/Scheduling/TimetableSnapshotStore.cs` | فك العبارات المضغوطة إلى تنسيق مقروء للدوال الحالية فقط؛ لا تغيير لترتيب الحذف والإدراج والتعامل مع الاستثناء والإلغاء | مراجعة diff كتغيير whitespace فقط، بناء الحل والفحوص المتاحة | منخفض جدًا |

ترشيحي: **A أولًا؛ B وC وD عند الرغبة بتحسين التنظيم والوضوح**. لا أحذف أي صنف أو دالة عامة. لا أوحّد writers ولا أنشئ helper أو interface أو service. لا تعديلات على UI أو الخوارزميات أو Models أو SQL الصيانة أو المعاملات. F06 وR01–R10 خارج نطاق هذه الموافقة.

إذا كانت سياسة المشروع تفضّل التصريح المباشر بحزمة Core لأنها مستخدمة مباشرة في DAL، يمكن الاحتفاظ بمرجعها؛ هي زائدة في graph الحالي وليست خطأ وظيفيًا. حجم التنظيف لا يبرر تغيير سياسة dependencies دون اتفاق.

## نتائج التحقق الحالية

| الفحص | النتيجة | ما يثبته وما لا يثبته |
|---|---|---|
| `dotnet build "University Timetable Management System.slnx" --no-restore --nologo` | نجح؛ 0 أخطاء، 0 تحذيرات، 2.99 ثانية | بناء المشاريع الخمسة من نسخة العمل الحالية وبالأصول الموجودة؛ ليس اختبار سلوك قاعدة البيانات |
| محاولة `dotnet run --project tests/PresentationBllRefactoring/PresentationBllRefactoring.csproj --no-restore --no-launch-profile` | تعذرت بسبب `NETSDK1004` وغياب assets لهذا الاختبار | مشكلة تجهيز أولية، وليست فشل assertions |
| إعادة التشغيل مع restore الافتراضي ودون تعديل المصدر | نجحت؛ exit code 0، **51 فحصًا، 0 فشل** | 9 فحوص تدفق المحرر والاستثناءات و42 فحصًا لمدخلات الهوية وترتيب تحقق BLL؛ لا اتصال SQL ولا تغطية DAL تكاملية |
| اختبارات SQL | لم تُشغّل | غالبها ينشئ قواعد/جداول بـEnsureCreated أو ينفّذ DDL؛ تشغيلها غير مناسب للقيد الحالي بعدم تغيير المخطط |
| CRUD وتحميل العلاقات الفعلي | مراجعة مصدر فقط؛ غير متحقق وقت التشغيل | لا ادعاء سلامة تكاملية كاملة |
| Commit/Rollback/identity في SQL Server | مراجعة مصدر واختبارات متاحة فقط؛ غير منفذ | يلزم قاعدة اختبار جاهزة ومعزولة تغطي مخططات قديمة وحديثة |
| DI | مراجعة التسجيل والمستهلكين وبناء ناجح؛ لم تُشغّل API | لم تُختبر service resolution والاستجابة end-to-end وقت التشغيل |
| استعلامات الجدولة | روجعت البيانات المحملة والقيود ومواضع الحفظ؛ لم يُشغّل التوليد | لا قياس خطط تنفيذ أو زمن/أقفال أو regression scheduling |

لا أعدّ `dotnet test` على ملف الحل إثباتًا لتشغيل الاختبارات: مشاريع الاختبار هنا أدوات Console منفصلة وغير مضمنة في الحل. لا يوجد أساس للقول إن جميع اختبارات النظام اجتازت.

## بروتوكول التحقق بعد كل دفعة معتمدة

1. التحقق من أن diff محصور في ملفات الدفعة؛ مقارنة العقود العامة ومحتوى إعداد النموذج وSQL، وحماية تعديلات المستخدم السابقة.
2. بناء الحل كاملًا، وتشغيل الفحوص الموجودة التي لا تغيّر قاعدة البيانات. عند تغيير references، إجراء restore وفحص dependency graph أولًا.
3. فحص DI والإنشاء الافتراضي ومنشئ options. التشغيل الكامل لـAPI مؤجل ما دام startup ينفّذ DDL على القاعدة المختارة.
4. توثيق حالة CRUD وتحميل العلاقات والمعاملات واستعلامات الجدولة بعد كل دفعة بصراحة: منفذ/غير منفذ، مع السبب. لا تُعدّ المراجعة الساكنة بديلًا للاختبار.
5. للتحقق التكاملي لاحقًا: استخدام قاعدة اختبار مجهزة ومعزولة دون إنشاء/apply migrations؛ توفير مسار لا ينفّذ setup DDL، أو الحصول على إذن مستقل لتجهيز بيئة اختبار. الأدوات الحالية لا توفر كلها هذا المسار، ولن أعدلها ضمنيًا لتجاوز شرطك.
6. عند توفر هذا المسار: CRUD للموارد والمستخدمين والتكاليف، تحميل العلاقات والعرض، rollback عند خطأ منتصف العملية والإلغاء، التبديل الدوري للمعرفات، الاستعادة الفارغة والكاملة، حفظ الأقفال والتفضيلات، واختبارات التوليد الإضافي وإعادة التوليد والبدائل والإصلاح.
7. مرشحات التغطية الموجودة: `ApplicationAudit`، `ExcelImport`، `FacultyScheduleEditor`، `FacultyPreferences`، `TimetableBackups`، `ImportedTimetableRepair`، `AssignmentScopes`، `AssignmentLectureTypes`، `SubjectLectureCounts`، `IncrementalGeneration`، `TimetableRegression` و`WebIntegration`. لا تُشغّل كما هي تلقائيًا؛ راجع setup والقاعدة المستهدفة أولًا.
8. أي اختلاف في العقد أو assets اللازمة أو سلوك مثبت يوقف الدفعة ويُعاد تغييرها فقط؛ لا إعادة ضبط شجرة العمل أو المساس بأعمال سابقة.

## التنفيذ والموافقة

بعد توصية تنفيذ A فقط أولًا، وافق المستخدم بقوله «نفذها اذن». نُفذت **A فقط**؛ لم تُنفذ B أو C أو D ولم تُضف أصناف أو واجهات أو مساعدات جديدة.

التغيير الوحيد في ملفات التطبيق: حذف السطر الذي يصرّح بالمرجع المباشر `Microsoft.EntityFrameworkCore` بالإصدار `8.0.28` من `Data_Access_Layer/Data_Access_Layer.csproj`. بقي مرجع `Microsoft.EntityFrameworkCore.SqlServer` بالإصدار نفسه. لم يُحذف EF Core من التبعيات المحلولة، ولم يتغير كود C# أو النموذج أو العلاقات أو SQL أو المعاملات.

نتائج الدفعة A:

| التحقق | النتيجة |
|---|---|
| `dotnet restore "University Timetable Management System.slnx" --nologo` | نجح، exit code 0 |
| مقارنة `targets` الخاصة بالحزم وبيانات `libraries` قبل restore وبعده | تطابق كامل لكل الحزم في DAL وBLL وAPI والواجهة: 55 و82 و82 و85 مدخل حزمة مستهدفًا على الترتيب؛ EF Core `8.0.28` موجودة في المشاريع الأربعة |
| بناء الحل كاملًا بالأمر السابق `--no-restore --nologo` | نجح، exit code 0، 0 أخطاء، 0 تحذيرات، 2.86 ثانية |
| تشغيل `PresentationBllRefactoring` مع restore الافتراضي | نجح، exit code 0، 51 فحصًا، 0 فشل |
| بصمات ملفات DAL العشرة من نوع C# | مطابقة لجرد التدقيق؛ لم يتغير أي منها |
| CRUD وتحميل العلاقات وcommit/rollback واستعلامات الجدولة وقت التشغيل | لم تُشغّل؛ تظل فجوة التحقق التكاملي المذكورة أعلاه قائمة |
| DI | التسجيل والعقود بلا تغيير، والبناء ناجح؛ لم تُشغّل API لأن startup ينفّذ DDL |

الأدلة الإضافية في `docs/Verification/DalAudit/`: `batch-a-dependencies-before.json` و`batch-a-dependency-comparison.json` و`batch-a-build.txt` و`batch-a-tests.txt` و`batch-a-checks.json`. يشمل snapshot السابق تفاصيل التبعيات المحلولة قبل التغيير، وتستبعد مقارنة الحزم تغيّر مرجع المشروع المتوقع نفسه.

لا توجد نتيجة اختبار تثبت سلامة تكاملية كاملة لقاعدة البيانات. الدليل الأقوى لهذه الدفعة أن أصول الحزم وإصداراتها لم تتغير، وأن تعديل التطبيق اقتصر على إزالة التصريح المكرر. جميع المسائل R01–R10 والتعديلات الاختيارية B–D بقيت دون تنفيذ.
