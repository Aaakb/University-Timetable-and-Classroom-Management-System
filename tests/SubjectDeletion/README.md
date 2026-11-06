# Delete a subject, all its assignments and all its lectures

Run on Windows with SQL Server (default `localhost`; override with
`UNIVERSITY_TIMETABLE_TEST_SERVER`):

```powershell
dotnet run --project tests/SubjectDeletion -c Release --artifacts-path C:/CodexBuilds/SubjectDeletion
```

Use a short output path on Windows so SqlClient's native library stays within
the platform's native DLL path limit.

The 19 SQL-backed checks use a randomly named
`UniversityTimetable_SubjectDeletion_*` database, never the configured application
database. Cleanup checks the exact generated database name before dropping it.

Coverage includes multiple teachers, general/section-scoped assignments, theory
and practical lectures, locked lectures, legacy rows with no assignment, already
archived subjects, missing subjects, repeated deletion, and subjects without
assignments. Exact snapshots prove unrelated subjects, assignments, timetable
rows and teachers remain unchanged.

Two injected failures reject assignment deletion and subject archival separately.
Both require the subject, assignments and previously deleted lectures to roll
back together. The original implementation fails 7 of these 19 checks.
