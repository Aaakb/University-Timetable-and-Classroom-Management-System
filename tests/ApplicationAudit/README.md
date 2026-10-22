# Full application audit

Run on Windows with .NET and SQL Server:

```powershell
dotnet run --project tests/ApplicationAudit/ApplicationAudit.csproj -c Release --artifacts-path D:/CodexAudit20261015
```

The harness reads the application's configured database only to make a full SQL Server copy-only backup and restore it under a fresh `UniversityTimetable_FourFixes_Audit_<random>` name. It sets the connection override to that copy before any service or screen test. It does not change the original database. The copy and backup are retained.

The harness compares the copied subject/lesson counts against the source counts read before the backup, and uses all copied records. The latest source has 96 subjects, 22 faculty (including 4 provisional teachers), 280 teaching assignments, 10 sections, 11 rooms, 3 periods, and 293 lessons. The 29 checks exercise add/update/delete services for each record type, account registration/sign-in, duplicate subject names in independent branches, saved room/section/assignment protection, invalid lengths and numeric values, complete generation and conflict/capacity checks, and actual editor selection/save inputs for every subject.

Results are in `.tmp/ApplicationAudit/results`. The runtime screen harness is `tests/DesignerCompatibility`; it supports `UNIVERSITY_TIMETABLE_TEST_SERVER` and checks all screens at 1280/1600, including the delayed-read/clear-form race. Its database argument must use the isolated `UniversityTimetable_FourFixes_` prefix.

Use a short artifact path: the repository's existing long Windows path plus the SQL native runtime subdirectory can exceed the Windows loader path limit. The updated application executable is `D:/CodexAudit20261015/bin/Presentation_Layer/release/Presentation_Layer.exe`.
