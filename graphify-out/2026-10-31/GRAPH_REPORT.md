# Graph Report - University-Timetable-and-Classroom-Management-System  (2026-10-26)

## Corpus Check
- 407 files · ~326,312 words
- Verdict: corpus is large enough that graph structure adds value.
- Unclassified: 26 file(s) not represented in the graph (top: .resx 16, .css 4, (none) 2)

## Summary
- 2999 nodes · 8857 edges · 189 communities (117 shown, 72 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 988 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Faculty Schedule Optimization
- Business Layer Integration
- Application Shell Authentication
- Excel Import Workflow
- Timetable Backup Restoration
- Entity Framework Data Access
- Netlify Demo Browser Tests
- Responsive Designer Controls
- SQL Reflection Test Harnesses
- Timetable Filtering Rendering
- Schedule Screen Operations
- Teaching Assignment Editor
- Scheduling Rules Verification
- Schedule Validation Persistence
- Academic Resource CRUD
- Subject Lifecycle Services
- Constraint Solver Quality
- Subject Editor Controls
- Assignment Lifecycle Protection
- Web Academic Resource Interface
- Import Catalog Normalization
- Database Maintenance Operations
- API Resource Infrastructure
- Classroom Study Year Editors
- Business Project Dependencies
- Timetable Generation Alternatives
- Academic Identity Validation
- Classroom Capacity Validation
- API Mutation Coordination
- Designer Layout Verification
- Batched Layout Rendering
- Excel Import Desktop Interface
- Desktop Test Project Dependencies
- Study Year Services
- Account Registration Authentication
- Schedule Requirement Matching
- Scheduling Import Test Dependencies
- Section Editor Controls
- Generation State Indexes
- Timetable Alternative API
- Schedule Details Projection
- Import Data Formatting
- Faculty Schedule Preview Models
- Scheduling Request Generation
- PDF Canvas Rendering
- Timetable Regression Harness
- Weekly Timetable Canvas
- Resource CRUD API
- Faculty Preference Regression
- Practical Exchange Planning
- Faculty Preferences Interface
- Layer Cleanup Documentation
- Web Data Transfer Interface
- Timetable Import Parsing
- Arabic User Messages
- Faculty Schedule Editor
- API Test Project Dependencies
- Import Authentication Endpoints
- Design Runtime Lifecycle Checks
- Excel Workbook Verification
- Weekly PDF Grid Parsing
- Placement Repair Search
- Alternative Timetable Interface
- Branch CRUD Services
- Display Row Numbering Tests
- Desktop Regression Documentation
- Branch Editor Actions
- Excel Schema Templates
- Designer Compatibility Checks
- Time Slot Editor
- Lecture Timing Rules
- PDF Page Construction
- Database Failure Interceptors
- Import Column Mapping
- Solution Project Naming
- Atomic Faculty Schedule Writing
- Final Faculty Cleanup
- PWA Static Demonstration
- Faculty Member Editor
- Subject Display Model
- Web API Day Preferences
- Academic Structure Rules
- Grid Row Numbering
- Shared Scheduling Workflow Documentation
- Imported Timetable Repair Tests
- Teaching Assignment Display Model
- Workbook Database Roundtrip
- Timetable Timeline Day Rules
- Import Repair Documentation
- Subject API Data Model
- Period Preferences Web Interface
- PDF Table Extraction
- Faculty Assignment Coverage
- Scheduling API Endpoints
- Academic Lookup Binding
- Schedule Filter API
- Schedule API Data Model
- PDF Import File Reading
- Scheduling Capacity Analysis
- PDF Verification Scripts
- Assignment API Data Model
- Alternative Timetable Web Interface
- Backup Restore API
- Schedule Build Configuration
- Instructor PDF Export Fixes
- University Application Architecture
- Section Display Model
- Faculty Preferences Documentation
- Editor Validation Regression
- Schedule Filter Criteria
- Arabic Interface Translation
- Native Solver Runtime
- Generation Result Model
- PWA Startup Configuration
- Section API Data Model
- Web Resource Schema
- Classroom API Data Model
- SQL Express Recovery
- Cleanup Preservation Evidence
- DAL Relationship Evidence
- API Error Middleware
- Import Verification Documentation
- Faculty API Data Model
- Faculty Preference Persistence
- DAL Audit Documentation
- Final Timetable Validation
- Responsive Layout Tests
- Application Audit Documentation
- BLL Readability Review
- Windows Solver Runtime Fix
- Database Connection Configuration
- Windows Designer Compatibility
- Designer Migration Documentation
- DPI Display Compatibility
- Dynamic Scheduling Resources
- Name Based Academic Imports
- Faculty Preference Proposal
- Faculty Schedule Editor Documentation
- Historical Fix Verification
- Flexible Lecture Periods
- Lecture Counts Assignment Scope
- Layout Editor Lifecycle Evidence
- Compilation Verification Limits
- Named Assignment Import Tests
- Data Transfer Page
- Advanced Preference Page
- Database Connection Provider
- Shared Laboratory Cleanup
- Arabic Interface Test Project
- Designer Compatibility Test Project
- Designer Lifecycle Test Project
- Designer Migration Test Project
- Excel Import Test Project
- Faculty Preference Test Project
- Full Generation Test Project
- Generation Diagnostics Test Project
- Incremental Generation Test Project
- PDF Export Test Project
- Practical Exchange Test Project
- Presentation Refactoring Test Project
- Responsive Layout Test Project
- Timetable Regression Test Project
- UI Opening Test Project
- Assignment Scope Upgrade Tool
- Faculty Day Import Tool
- Alternative Timetable Page
- Subject Requirement Compatibility
- BLL Structural Method Inventory
- Historical Working Tree Snapshot
- Assignment Component Verification
- Strict Profile Generation Regression
- Practical Exchange Regression Documentation
- PDF Render Verification Documentation
- Production Planner Capacity Probe
- Weekly Lecture Count Regression
- Web Transfer Lifecycle Checks
- Weekly PDF Roundtrip Verification
- Small Calendar Application Icon
- Large Calendar Application Icon
- Vector Calendar Application Icon
- PWA Service Worker Assets

## God Nodes (most connected - your core abstractions)
1. `AppDbContext` - 179 edges
2. `Schedule` - 177 edges
3. `SchedulesForm` - 157 edges
4. `University_Timetable_and_Classroom_Management_System.BusinessLayer` - 131 edges
5. `University_Timetable_and_Classroom_Management_System.Models` - 111 edges
6. `Subject` - 84 edges
7. `University_Timetable_and_Classroom_Management_System` - 80 edges
8. `TimeSlot` - 74 edges
9. `Section` - 73 edges
10. `Data_Access_Layer` - 68 edges

## Surprising Connections (you probably didn't know these)
- `PWA` --semantically_similar_to--> `PWA`  [INFERRED] [semantically similar]
  README.md → UniversityTimetable.API/wwwroot/index.html
- `Optional day preferences` --semantically_similar_to--> `Optional day preferences`  [INFERRED] [semantically similar]
  UniversityTimetable.API/wwwroot/preferences.html → docs/FacultyPreferences.md
- `Atomic preference rescheduling` --semantically_similar_to--> `Atomic preference rescheduling`  [INFERRED] [semantically similar]
  UniversityTimetable.API/wwwroot/preferences.html → docs/FacultyPreferences.md
- `Candidate` --references--> `Schedule`  [EXTRACTED]
  Business_Logic_Layer/FacultyScheduling/FacultyCpSatPlanner.cs → Models/Entities/Schedule.cs
- `Unit` --references--> `Schedule`  [EXTRACTED]
  Business_Logic_Layer/FacultyScheduling/FacultyPracticalExchange.cs → Models/Entities/Schedule.cs

## Import Cycles
- None detected.

## Communities (189 total, 72 thin omitted)

### Community 0 - "Faculty Schedule Optimization"
Cohesion: 0.06
Nodes (42): FacultyScheduleEditorService, Preferences, FacultyAvailabilityMode, Preferred, Strict, FacultyDayPreferenceResult, FacultyEditorLookups, FacultyEditorOption (+34 more)

### Community 1 - "Business Layer Integration"
Cohesion: 0.07
Nodes (3): University_Timetable_and_Classroom_Management_System, University_Timetable_and_Classroom_Management_System.BusinessLayer, University_Timetable_and_Classroom_Management_System.Models

### Community 2 - "Application Shell Authentication"
Cohesion: 0.05
Nodes (19): FormNavigation, NavigationItem, NavigationPage, Branches, Classrooms, FacultyAssignments, FacultyMembers, Schedules (+11 more)

### Community 3 - "Excel Import Workflow"
Cohesion: 0.07
Nodes (32): AcademicImportRow, ExcelImportPreview, AcademicRows, Changes, ContentVersion, DatabaseVersion, GenerationCompleted, Issues (+24 more)

### Community 4 - "Timetable Backup Restoration"
Cohesion: 0.08
Nodes (25): TimetableBackupDocument, TimetableBackupInfo, TimetableBackupLecture, TimetableBackupPayload, TimetableBackupReference, TimetableRestorePreview, CanRestore, CreatedAtUtc (+17 more)

### Community 5 - "Entity Framework Data Access"
Cohesion: 0.07
Nodes (10): Data_Access_Layer, ImportTransaction, ScheduleLockSchema, FacultyAssignmentScopeSchema, FacultyPreferenceSchema, SubjectDeletionSchema, SubjectLectureCountSchema, TimetableSnapshotStore (+2 more)

### Community 6 - "Netlify Demo Browser Tests"
Cohesion: 0.07
Nodes (30): all(), api(), ApiError, clone(), currentLectures(), details(), envelope(), key() (+22 more)

### Community 7 - "Responsive Designer Controls"
Cohesion: 0.08
Nodes (10): DesignerHtmlLabel, AvoidGeometryAntialias, IsContextMenuEnabled, IsSelectionEnabled, PlainText, UseGdiPlusTextRendering, UsesArabicText, ResponsiveCardPanel (+2 more)

### Community 8 - "SQL Reflection Test Harnesses"
Cohesion: 0.11
Nodes (4): Assignment, CaseResult, Fixture, Requested

### Community 9 - "Timetable Filtering Rendering"
Cohesion: 0.07
Nodes (30): ScheduleFilterService, TimetableCell, TimetableSlot, ScheduleRow, BranchID, BranchName, ClassroomID, ClassroomName (+22 more)

### Community 10 - "Schedule Screen Operations"
Cohesion: 0.09
Nodes (5): SchedulesForm, TimetableCardHeight, TimetableRowHeight, TimetableStep, IsScheduleUiUnavailable

### Community 12 - "Scheduling Rules Verification"
Cohesion: 0.05
Nodes (40): 263-lesson isolated validation, Complete constraint fallback, Complete regeneration with faculty day preferences, ScheduleService, Strict faculty availability, AcademicStructureRules, ClassroomRules, Conditional four-day section limit (+32 more)

### Community 13 - "Schedule Validation Persistence"
Cohesion: 0.08
Nodes (16): ScheduleConflictChecker, ScheduleService, AppDbContext, ApplicationUsers, Branches, Classrooms, FacultyMembers, FacultyMemberSubjects (+8 more)

### Community 14 - "Academic Resource CRUD"
Cohesion: 0.16
Nodes (7): AutoKeyGenerator, ClassroomService, FacultyMemberService, SectionService, TimeSlotService, DefaultLecturePeriod, ResourceCatalog

### Community 15 - "Subject Lifecycle Services"
Cohesion: 0.08
Nodes (24): SubjectService, Subject, Branch, BranchID, CreditUnits, DefaultPracticalLectureCount, DefaultTheoreticalLectureCount, EffectivePracticalLectureCount (+16 more)

### Community 16 - "Constraint Solver Quality"
Cohesion: 0.11
Nodes (12): Candidate, FacultyCpSatPlanner, SearchLimitException, FacultyPracticalExchange, Unit, PreferenceExpressions, QualityExpressions, SchedulingCpSupport (+4 more)

### Community 18 - "Assignment Lifecycle Protection"
Cohesion: 0.14
Nodes (11): FacultyMemberSubjectService, FacultyMemberSubject, FacultyMember, FacultyMemberID, LectureType, Section, SectionID, Subject (+3 more)

### Community 19 - "Web Academic Resource Interface"
Cohesion: 0.15
Nodes (32): closeEditor(), confirmAction(), deleteRow(), display(), enter(), error(), esc(), exportPdf() (+24 more)

### Community 20 - "Import Catalog Normalization"
Cohesion: 0.13
Nodes (13): AcademicExcelImport, ImportCatalog, Tables, ScheduleRoomSelector, Section, Branch, BranchID, Schedules (+5 more)

### Community 22 - "API Resource Infrastructure"
Cohesion: 0.12
Nodes (7): UniversityTimetable.API.Controllers, UniversityTimetable.API, ImportUiText, ImportGeneration, ImportSheetInput, ImportValidation, Program

### Community 23 - "Classroom Study Year Editors"
Cohesion: 0.13
Nodes (3): ClassroomsForm, DesignerEnvironment, StudyYearsForm

### Community 24 - "Business Project Dependencies"
Cohesion: 0.07
Nodes (22): net8.0, Microsoft.NET.Sdk, ClosedXML (0.105.0), Google.OrTools (9.15.6755), Microsoft.EntityFrameworkCore (8.0.28), PdfPig (0.1.11), SkiaSharp (2.88.9), Topten.RichTextKit (0.5.169) (+14 more)

### Community 25 - "Timetable Generation Alternatives"
Cohesion: 0.15
Nodes (11): FacultyPreferenceMeasurement, FacultyPreferenceMeasurements, FacultyPreferenceRules, TimetableAlternativesService, TeachingDays, TimeSlot, EndTime, IsBreak (+3 more)

### Community 26 - "Academic Identity Validation"
Cohesion: 0.11
Nodes (9): ManualKeySaveHelper, SchedulingResourceMaintenance, InputValidation, FacultyMember, AcademicTitle, FacultyMemberID, FacultyMemberSubjects, FullName (+1 more)

### Community 27 - "Classroom Capacity Validation"
Cohesion: 0.12
Nodes (9): ClassroomRules, Classroom, Capacity, ClassroomID, ClassroomNumber, RoomType, Schedules, Program (+1 more)

### Community 28 - "API Mutation Coordination"
Cohesion: 0.11
Nodes (11): ApiFailure, Status, MutationGate, Release, CachedPreview, CommitFacultyPreferences, DeleteFacultyPreferences, FacultyPreferencesController (+3 more)

### Community 29 - "Designer Layout Verification"
Cohesion: 0.15
Nodes (3): DesignContext, UsageMode, Program

### Community 30 - "Batched Layout Rendering"
Cohesion: 0.12
Nodes (5): DesignContext, UsageMode, Program, LayoutUpdateScope, WindowSizing

### Community 31 - "Excel Import Desktop Interface"
Cohesion: 0.14
Nodes (6): ExcelImportForm, HasSavedChanges, Mode, SelectedSheet, ImportValuesForUi, MappingChoice

### Community 32 - "Desktop Test Project Dependencies"
Cohesion: 0.07
Nodes (19): Guna.UI2.WinForms (2.0.4.8), net8.0-windows, Microsoft.NET.Sdk, net8.0-windows, Microsoft.NET.Sdk, net8.0-windows, Microsoft.NET.Sdk, net8.0-windows (+11 more)

### Community 33 - "Study Year Services"
Cohesion: 0.12
Nodes (11): StudyYearService, StudyYear, Schedules, Sections, StudyYearID, Subjects, YearName, OverviewController (+3 more)

### Community 34 - "Account Registration Authentication"
Cohesion: 0.11
Nodes (16): AuthResult, AuthService, ApplicationUser, ApplicationUserID, CreatedAtUtc, FullName, NormalizedUserName, PasswordHash (+8 more)

### Community 35 - "Schedule Requirement Matching"
Cohesion: 0.08
Nodes (24): SchedulePlanningResult, PracticalExchangeIssues, ScheduleRequirementMatcher, Schedule, Branch, BranchID, Classroom, ClassroomID (+16 more)

### Community 38 - "Generation State Indexes"
Cohesion: 0.17
Nodes (3): ScheduleGenerationState, Schedules, ArabicUiText

### Community 39 - "Timetable Alternative API"
Cohesion: 0.11
Nodes (21): TimetableAlternative, Lectures, Metrics, Plan, SearchSeconds, Semester, Status, Title (+13 more)

### Community 40 - "Schedule Details Projection"
Cohesion: 0.08
Nodes (23): ScheduleDetailsView, BranchID, BranchName, Capacity, ClassroomID, ClassroomNumber, DayOfWeek, EndTime (+15 more)

### Community 42 - "Faculty Schedule Preview Models"
Cohesion: 0.09
Nodes (23): Result, Options, FacultyAvailableSlot, FacultySchedulePreview, AffectedFacultyCount, AfterQuality, AvailableSlots, BeforeQuality (+15 more)

### Community 43 - "Scheduling Request Generation"
Cohesion: 0.21
Nodes (7): ScheduleDayPlan, ScheduleDayPlanKey, ScheduleGenerationRequest, SchedulePlacementOption, ScheduleRequestIdentity, SubjectTeachingAssignment, ScheduleGenerator

### Community 47 - "Resource CRUD API"
Cohesion: 0.18
Nodes (3): ResourcesController, IResource, Resource

### Community 49 - "Practical Exchange Planning"
Cohesion: 0.16
Nodes (8): Result, Diagnostics, FreshTimetablePlanner, Result, GeneratorProbe, ProbeResult, PracticalExchangeChecks, Program

### Community 51 - "Faculty Preferences Interface"
Cohesion: 0.20
Nodes (3): FacultyPreferencesForm, FacultyChoice, HasSavedChanges

### Community 52 - "Layer Cleanup Documentation"
Cohesion: 0.09
Nodes (22): AppDbContext direct service access, DatabaseHealthService, Designer resources preservation, Layer cleanup audit 14 October 2026, UNUSED_CLEANUP_REPORT, Unused repositories, 538 equivalent rule cases, AcademicStructureRules.IsGeneralSectionValid (+14 more)

### Community 53 - "Web Data Transfer Interface"
Cohesion: 0.18
Nodes (17): configureSheet(), dayNames, esc(), invalidate(), message(), mode(), renderPreview(), renderSheets() (+9 more)

### Community 56 - "Faculty Schedule Editor"
Cohesion: 0.15
Nodes (3): FacultyScheduleEditorForm, Choice, HasSavedChanges

### Community 57 - "API Test Project Dependencies"
Cohesion: 0.10
Nodes (14): Microsoft.NET.Sdk.Web, net8.0, Microsoft.NET.Sdk, net8.0-windows, Microsoft.NET.Sdk, net8.0-windows, Microsoft.NET.Sdk, net8.0-windows (+6 more)

### Community 58 - "Import Authentication Endpoints"
Cohesion: 0.16
Nodes (6): AuthController, LoginRequest, RegisterRequest, ImportCommit, ImportsController, Owner

### Community 59 - "Design Runtime Lifecycle Checks"
Cohesion: 0.16
Nodes (9): DesignContext, UsageMode, Program, Program, DesignContext, UsageMode, DesignContext, UsageMode (+1 more)

### Community 60 - "Excel Workbook Verification"
Cohesion: 0.15
Nodes (10): ExcelSheetData, IsWeeklyTimetable, Name, SourceName, SourceRows, StudySession, SuggestedSchemaName, Table (+2 more)

### Community 61 - "Weekly PDF Grid Parsing"
Cohesion: 0.26
Nodes (5): Box, Height, Width, Day, WeeklyPdfTimetableReader

### Community 63 - "Alternative Timetable Interface"
Cohesion: 0.16
Nodes (3): TimetableAlternativesForm, HasSavedChanges, ChangeTimetableLocks

### Community 64 - "Branch CRUD Services"
Cohesion: 0.16
Nodes (10): BranchService, Branch, BranchID, BranchName, Schedules, Sections, Subjects, BranchDto (+2 more)

### Community 65 - "Display Row Numbering Tests"
Cohesion: 0.28
Nodes (4): DesignContext, UsageMode, KeyRow, RowNumberingChecks

### Community 66 - "Desktop Regression Documentation"
Cohesion: 0.12
Nodes (20): Complete isolated database audit, Full application audit, Arabic interfaces, Canonical Arabic UI translation, Designer compatibility regression, Runtime UI regression and DPI limitations, Designer label lifecycle regression, Guna HTML label disposal compatibility (+12 more)

### Community 68 - "Excel Schema Templates"
Cohesion: 0.15
Nodes (13): ExcelImportMode, AcademicData, CompleteTimetable, Hybrid, RepairTimetable, ExcelImportSchema, Academic, AcademicInputs (+5 more)

### Community 71 - "Lecture Timing Rules"
Cohesion: 0.14
Nodes (8): Period, ScheduleTimingRules, DefaultLectureSlots, TimeDisplay, TimeSlotDto, EndTime, StartTime, TimeSlotID

### Community 72 - "PDF Page Construction"
Cohesion: 0.22
Nodes (4): SchedulePdfExportOptions, SchedulePdfPage, SchedulePdfPageKey, SchedulePdfRow

### Community 73 - "Database Failure Interceptors"
Cohesion: 0.22
Nodes (5): FailureInterceptor, FailDayScheduleWrite, Fault, Fault, Fault

### Community 74 - "Import Column Mapping"
Cohesion: 0.23
Nodes (3): ImportLabels, NamedAssignmentImport, TransferFiles

### Community 75 - "Solution Project Naming"
Cohesion: 0.13
Nodes (13): net8.0, Microsoft.NET.Sdk, Official project name update, Presentation_Layer, PWA cache revision, University Timetable Management System, UniversityTimetable.API, Models (+5 more)

### Community 77 - "Final Faculty Cleanup"
Cohesion: 0.12
Nodes (17): 145 semester-2 lessons, 50 retained faculty, 77 semester-2 assignments, Faculty cleanup 18 October 2026, ScheduleService validation, SQL backup verification, 132 semester-1 lessons, 277 total lessons (+9 more)

### Community 78 - "PWA Static Demonstration"
Cohesion: 0.12
Nodes (16): 80 fictional lessons, Browser localStorage demo, Netlify static mock demonstration, NetlifyDemo checks, Server-dependent workflows disabled, AppDbContext, AuthService PBKDF2 SHA256, Cross-process concurrency limitation (+8 more)

### Community 80 - "Subject Display Model"
Cohesion: 0.12
Nodes (16): SubjectRow, BranchID, BranchName, CreditUnits, PracticalHours, PracticalLectureCount, PracticalLectureOverride, SemesterDisplay (+8 more)

### Community 81 - "Web API Day Preferences"
Cohesion: 0.22
Nodes (12): all(), api(), ApiError, refreshCsrf(), actions(), edited(), esc(), load() (+4 more)

### Community 84 - "Shared Scheduling Workflow Documentation"
Cohesion: 0.12
Nodes (16): Historical timetable PDF analysis, Independent branch capacity, Timetable preservation October 19, Incremental conflict repair, Timetable recovery October 19, Verified timetable restoration, Timetable alternatives, Preview and atomic approval (+8 more)

### Community 86 - "Teaching Assignment Display Model"
Cohesion: 0.12
Nodes (16): AssignmentRow, BranchID, BranchName, FacultyMemberID, FacultyMemberName, IsSubjectDeleted, LectureType, LectureTypeName (+8 more)

### Community 87 - "Workbook Database Roundtrip"
Cohesion: 0.22
Nodes (9): ExcelWorkbookData, Sheets, WeeklyVerification, Session, Mode, Owner, Preview, Revision (+1 more)

### Community 88 - "Timetable Timeline Day Rules"
Cohesion: 0.25
Nodes (3): ScheduleDayRules, AllDays, PdfTimeBlock

### Community 89 - "Import Repair Documentation"
Cohesion: 0.14
Nodes (14): CP-SAT minimal-change objective, Data fingerprint checks, Imported timetable repair, ImportedTimetableRepairPlanner, PDF without OCR, ScheduleID identity preservation, Serializable transaction, Data fingerprint validation (+6 more)

### Community 90 - "Subject API Data Model"
Cohesion: 0.14
Nodes (12): SubjectDto, BranchID, CreditUnits, PracticalHours, PracticalLectureCount, RequirementType, SemesterNumber, StudyYearID (+4 more)

### Community 91 - "Period Preferences Web Interface"
Cohesion: 0.29
Nodes (12): cellSelect(), edited(), esc(), invalidate(), levels, load(), loadLookups(), matrix() (+4 more)

### Community 92 - "PDF Table Extraction"
Cohesion: 0.28
Nodes (3): Cell, Line, PdfTableReader

### Community 93 - "Faculty Assignment Coverage"
Cohesion: 0.22
Nodes (3): FacultyAssignmentRules, SubjectLectureRules, ScheduleFacultySelector

### Community 94 - "Scheduling API Endpoints"
Cohesion: 0.26
Nodes (4): Versions, GenerateRequest, SchedulingController, SetLectureLock

### Community 96 - "Schedule Filter API"
Cohesion: 0.17
Nodes (11): ScheduleFilter, BranchId, ClassroomId, DayOfWeek, FacultyMemberId, GroupName, LectureType, Q (+3 more)

### Community 97 - "Schedule API Data Model"
Cohesion: 0.15
Nodes (11): ScheduleDto, ClassroomID, DayOfWeek, FacultyMemberID, GroupName, IsLocked, LectureType, ScheduleID (+3 more)

### Community 99 - "Scheduling Capacity Analysis"
Cohesion: 0.17
Nodes (12): 293 planned lessons, 96 provisional-stage subjects, Provisional subject corrections 15 October 2026, Four temporary subject records, Independent temporary faculty, Scheduling capacity verification, 165 room bookings per semester, 314-lesson successful stress plan (+4 more)

### Community 101 - "Assignment API Data Model"
Cohesion: 0.17
Nodes (10): AssignmentDto, BranchID, FacultyMemberID, IsSubjectDeleted, LectureType, SectionID, SemesterNumber, StudyYearID (+2 more)

### Community 102 - "Alternative Timetable Web Interface"
Cohesion: 0.36
Nodes (11): changeLocks(), days, details(), esc(), load(), options, render(), run() (+3 more)

### Community 103 - "Backup Restore API"
Cohesion: 0.25
Nodes (5): DownloadEntry, PreviewEntry, TimetableBackupsController, Owner, TransferConfirmation

### Community 105 - "Schedule Build Configuration"
Cohesion: 0.20
Nodes (10): ScheduleBuildContext, CancellationToken, EnforceSectionDayLimit, FixedSchedules, Preferences, RepairAttemptLimit, RepairTimeLimitSeconds, RequirePracticalExchange (+2 more)

### Community 106 - "Instructor PDF Export Fixes"
Cohesion: 0.20
Nodes (10): Instructor PDF export grouping fix, Five-day RTL layout, Instructor-filtered semester pages, SchedulePdfExport tests, SchedulePdfExportService, PDF native library long-path fix, HarfBuzzSharp native loading, PdfExport tests (+2 more)

### Community 107 - "University Application Architecture"
Cohesion: 0.20
Nodes (10): PWA, University Timetable Management System, UniversityTimetable, Windows Forms, Offline connectivity, PWA, Shared accounts, University web application (+2 more)

### Community 108 - "Section Display Model"
Cohesion: 0.20
Nodes (8): SectionRow, BranchID, BranchName, SectionID, SectionName, StudentCount, StudyYearID, StudyYearName

### Community 110 - "Faculty Preferences Documentation"
Cohesion: 0.25
Nodes (9): Atomic preference rescheduling, Faculty preferences documentation, Four-day section limit, Legacy period preference compatibility, Optional day preferences, Advanced period preferences, Atomic preference rescheduling, Day preferences web page (+1 more)

### Community 113 - "Schedule Filter Criteria"
Cohesion: 0.22
Nodes (8): ScheduleFilterCriteria, DayOfWeek, FacultyId, GroupName, LectureType, SectionId, SemesterNumber, StudyYearId

### Community 116 - "Generation Result Model"
Cohesion: 0.25
Nodes (7): ScheduleGenerationResult, AddedCount, IsRegenerated, PreservedCount, RepairedCount, ReplacedCount, SafetyBackupPath

### Community 117 - "PWA Startup Configuration"
Cohesion: 0.25
Nodes (8): Cross-process concurrency limitation, database-settings.json, DatabaseConnection, PWA startup guide, Service Worker updates, Start-Web.cmd, UNIVERSITY_TIMETABLE_CONNECTION_STRING, WebIntegration tests

### Community 118 - "Section API Data Model"
Cohesion: 0.25
Nodes (6): SectionDto, BranchID, SectionID, SectionName, StudentCount, StudyYearID

### Community 119 - "Web Resource Schema"
Cohesion: 0.25
Nodes (3): days, modules, types

### Community 120 - "Classroom API Data Model"
Cohesion: 0.29
Nodes (5): ClassroomDto, Capacity, ClassroomID, ClassroomNumber, RoomType

### Community 121 - "SQL Express Recovery"
Cohesion: 0.33
Nodes (6): 60-second restart recovery, Automatic service start, SQL Express startup recovery fix, MSSQL SQLEXPRESS, UniversityTimetable connectivity, Unresolved SQL crash root cause

### Community 122 - "Cleanup Preservation Evidence"
Cohesion: 0.33
Nodes (6): Designer compatibility preservation, Unused source cleanup, Presentation BLL verification evidence, Preserved source and output hashes, Display row identity separation, Row numbering release tests

### Community 123 - "DAL Relationship Evidence"
Cohesion: 0.33
Nodes (6): AppDbContext integration inventory, DAL consumer evidence, Academic entity relationships, Entity consumer evidence, Query and transaction evidence, Serializable atomic persistence

### Community 125 - "Import Verification Documentation"
Cohesion: 0.33
Nodes (6): Atomic Excel import regression, Excel import verification, Atomic imported timetable repair regression, Imported timetable repair verification, Deterministic PDF fixture and UI verification, PDF import verification

### Community 126 - "Faculty API Data Model"
Cohesion: 0.33
Nodes (4): FacultyMemberDto, AcademicTitle, FacultyMemberID, FullName

### Community 128 - "DAL Audit Documentation"
Cohesion: 0.40
Nodes (5): AppDbContext, DAL audit report, EF Core dependency cleanup, Schema compatibility helpers, Serializable transactions

### Community 129 - "Final Timetable Validation"
Cohesion: 0.40
Nodes (5): Final timetable validation, ScheduleService validation, Startup preserves 277 lessons, Subgroup overlap prevention, Whole-section practical capacity

### Community 131 - "Application Audit Documentation"
Cohesion: 0.50
Nodes (4): Application audit 2026-10-15, Isolated test databases, Schedule integrity, Serializable manual lecture save

### Community 132 - "BLL Readability Review"
Cohesion: 0.50
Nodes (4): BLL readability review, BuildTimetableAsync, CP-SAT planner readability, Preserve validation and transactions

### Community 133 - "Windows Solver Runtime Fix"
Cohesion: 0.50
Nodes (4): check-cp-sat, CP-SAT Windows runtime fix, FacultySchedulingRuntime, LoadLibraryExW

### Community 134 - "Database Connection Configuration"
Cohesion: 0.50
Nodes (4): API host configuration, Database connection configuration, database-settings.json, UNIVERSITY_TIMETABLE_CONNECTION_STRING

### Community 135 - "Windows Designer Compatibility"
Cohesion: 0.50
Nodes (4): Async UI disposal guards, DesignerHtmlLabel, Row numbering, Windows Forms Designer compatibility

### Community 136 - "Designer Migration Documentation"
Cohesion: 0.50
Nodes (4): Design mode isolation, Designer.cs separation, Designer migration 2026-10-25, Designer round trip limitation

### Community 137 - "DPI Display Compatibility"
Cohesion: 0.50
Nodes (4): Display compatibility, DPI simulation limits, PerMonitorV2, Scrollable responsive layout

### Community 138 - "Dynamic Scheduling Resources"
Cohesion: 0.50
Nodes (4): Dynamic resources review 2026-10-17, Explicit classroom type, New database defaults, Persisted scheduling resources

### Community 139 - "Name Based Academic Imports"
Cohesion: 0.50
Nodes (4): Excel faculty and subject import, Name-only Excel import, No teaching assignments, Unclassified subjects

### Community 140 - "Faculty Preference Proposal"
Cohesion: 0.50
Nodes (4): Faculty preference proposed specification, Fixed attendance plan proposal, Preference objective proposal, Three-level period preference proposal

### Community 141 - "Faculty Schedule Editor Documentation"
Cohesion: 0.50
Nodes (4): Faculty schedule editor documentation, FacultyCpSatPlanner, Practical exchange constraints, Serializable schedule save

### Community 142 - "Historical Fix Verification"
Cohesion: 0.50
Nodes (4): Arabic PDF export, Empty generation rejection, Four fixes verification 2026-10-10, Semester migration

### Community 143 - "Flexible Lecture Periods"
Cohesion: 0.50
Nodes (4): Additional saved periods, Flexible lecture periods, Ten-minute break, TimeSlot duration

### Community 144 - "Lecture Counts Assignment Scope"
Cohesion: 0.50
Nodes (4): Archived subject preservation, Weekly lecture counts, Teaching assignment scope, Section and lecture type coverage

### Community 145 - "Layout Editor Lifecycle Evidence"
Cohesion: 0.50
Nodes (4): Batched Arabic layout, UI opening performance, DAL audit batch tests, Editor lifecycle and record IDs

### Community 146 - "Compilation Verification Limits"
Cohesion: 0.50
Nodes (4): DAL audit baseline, Runtime verification limits, DAL audit batch build, Successful compilation evidence

### Community 148 - "Data Transfer Page"
Cohesion: 0.50
Nodes (4): Data transfer web page, Excel academic import, Timetable backup, Timetable restore

### Community 149 - "Advanced Preference Page"
Cohesion: 0.50
Nodes (4): Advanced period preferences web page, Attendance plan, Optimization preview, Three period preference levels

### Community 151 - "Shared Laboratory Cleanup"
Cohesion: 0.67
Nodes (3): Code cleanup 2026-10-13, Shared laboratory policy, Unused UI helpers

### Community 169 - "Alternative Timetable Page"
Cohesion: 0.67
Nodes (3): Alternative timetables web page, Locked lectures, Timetable alternatives

## Knowledge Gaps
- **790 isolated node(s):** `CreatedAtUtc`, `LectureCount`, `CurrentLectureCount`, `SemesterOneCount`, `SemesterTwoCount` (+785 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 959 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **72 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Schedule` connect `Schedule Requirement Matching` to `Faculty Schedule Optimization`, `Excel Import Workflow`, `Timetable Backup Restoration`, `Schedule Validation Persistence`, `Subject Lifecycle Services`, `Constraint Solver Quality`, `Assignment Lifecycle Protection`, `Import Catalog Normalization`, `Timetable Generation Alternatives`, `Academic Identity Validation`, `Classroom Capacity Validation`, `API Mutation Coordination`, `Study Year Services`, `Generation State Indexes`, `Timetable Alternative API`, `Faculty Schedule Preview Models`, `Scheduling Request Generation`, `Timetable Regression Harness`, `Faculty Preference Regression`, `Practical Exchange Planning`, `Timetable Import Parsing`, `Placement Repair Search`, `Alternative Timetable Interface`, `Branch CRUD Services`, `Database Failure Interceptors`, `Atomic Faculty Schedule Writing`, `Imported Timetable Repair Tests`, `Faculty Assignment Coverage`, `Schedule API Data Model`, `Schedule Manual Editing`, `Schedule Build Configuration`?**
  _High betweenness centrality (0.073) - this node is a cross-community bridge._
- **Are the 77 inferred relationships involving `AppDbContext` (e.g. with `.ApplyPendingFixesAsync()` and `.ApplyAsync()`) actually correct?**
  _`AppDbContext` has 77 INFERRED edges - model-reasoned connections that need verification._
- **What connects `CreatedAtUtc`, `LectureCount`, `CurrentLectureCount` to the rest of the system?**
  _790 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Faculty Schedule Optimization` be split into smaller, more focused modules?**
  _Cohesion score 0.06009615384615385 - nodes in this community are weakly interconnected._
- **Why does `SchedulesForm` connect `Schedule Screen Operations` to `Business Layer Integration`, `Application Shell Authentication`, `Timetable Backup Restoration`, `Timetable Filtering Rendering`, `Teaching Assignment Editor`, `Schedule Validation Persistence`, `Academic Resource CRUD`, `Subject Lifecycle Services`, `Subject Editor Controls`, `Assignment Lifecycle Protection`, `Import Catalog Normalization`, `Classroom Study Year Editors`, `Academic Identity Validation`, `Classroom Capacity Validation`, `Batched Layout Rendering`, `Study Year Services`, `Generation State Indexes`, `PDF Canvas Rendering`, `Weekly Timetable Canvas`, `Practical Exchange Planning`, `Schedule Selection Binding`, `Design Runtime Lifecycle Checks`, `Alternative Timetable Interface`, `Branch CRUD Services`, `Display Row Numbering Tests`, `Atomic Faculty Schedule Writing`, `Academic Structure Rules`, `Academic Lookup Binding`, `Schedule Manual Editing`, `Grid Configuration Helpers`, `Arabic Interface Translation`?**
  _High betweenness centrality (0.072) - this node is a cross-community bridge._
- **Are the 6 inferred relationships involving `Schedule` (e.g. with `.RunAsync()` and `.RunAsync()`) actually correct?**
  _`Schedule` has 6 INFERRED edges - model-reasoned connections that need verification._
- **Should `Business Layer Integration` be split into smaller, more focused modules?**
  _Cohesion score 0.06830601092896176 - nodes in this community are weakly interconnected._

## Run limitations
Host-agent token usage was not exposed by the agent tools; reported zero semantic tokens means unavailable, not free. Four Excel templates were skipped because Office conversion support is absent. Twenty code files yielded no symbols. See .graphify_health.json for graph integrity diagnostics.

graphify-out/ was written into the working tree and is not ignored; review git status before committing.
