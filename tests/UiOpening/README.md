# UI opening layout regression

Run on Windows after building the desktop application:

```powershell
dotnet build tests/UiOpening -c Release
dotnet tests/UiOpening/bin/Release/net8.0-windows/UiOpening.dll 'University Timetable and Classroom Management System/bin/Release/net8.0-windows/Presentation_Layer.dll' '.tmp/UiOpening/layout-checks'
```

The harness constructs the nine navigation pages in a design-time license
context, applies the actual Arabic setup, and counts layout events throughout
each hierarchy. A screen-wide setup must remain within two layout events per
control. This detects the previous repeated ancestor layout without relying on
machine-dependent stopwatch thresholds. It also verifies Arabic/RTL setup and
that normal layout resumes afterward. It never shows an application form or
loads a database.

Elapsed times are diagnostic only. `checks.json` records the counts and timings;
a failed assertion returns a nonzero exit code. Existing ArabicInterfaces,
DesignerLifecycle, DesignerMigration, DesignerCompatibility, ResponsiveLayout,
and TimetableRegression navigation checks cover language, appearance, events,
resizing, and navigation while reads are pending.

Database UI checks require an isolated `UniversityTimetable_FourFixes_*` database.
Set `UNIVERSITY_TIMETABLE_TEST_SERVER` when it is on a named SQL Server instance.
On this machine that instance is `.\SQLEXPRESS`; do not substitute the application's
production database. The navigation safety test takes temporary read locks in
the isolated database to reproduce delayed loading.
