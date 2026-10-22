# Faculty preference verification

Run on Windows with .NET and SQL Server, from the repository root:

```powershell
dotnet build "University Timetable and Classroom Management System.slnx"
dotnet run --project tests/FacultyPreferences/FacultyPreferences.csproj -- ui web alternatives-ui
```

The suite creates a uniquely named `UniversityTimetable_Preferences_<guid>` database on `localhost` (override with `UNIVERSITY_TIMETABLE_TEST_SERVER`). It never uses the application's normal database. The database is retained for inspection. The web check launches a hidden process on an available loopback port using that same isolated database and stops it after verification. It creates a test administrator solely for authentication verification.

Coverage includes additive repeatable schema upgrades, three-level persistence and SQL uniqueness/check constraints, stable two-day/three-day attendance, prohibited periods, degree changes, locks, bounded movement of other faculty, metrics, stale updates and previews, cancellation, reproducible searches, initial generation, imported repair, whole-section practical compatibility, and the web authentication/CSRF/preview/commit lifecycle. UI checks cover 800x620 and 1180x820, preview invalidation, preference saving without timetable mutation, and visible actions.

Artifacts: `output/faculty-preferences-verification/results.json`, `before-after.json`, and `preferences-800.png` / `preferences-1180.png`.

Whole-semester alternatives additionally verify genuinely different time assignments, selection and atomic approval, stable IDs, no worse baseline preference penalty, retained attendance days, fixed imported lectures, explicit selective unlocking, stale locks and preferences, cancellation, and a unique feasible timetable. Alternative UI screenshots are `alternatives-900.png` and `alternatives-1180.png`; comparison data is `timetable-alternatives.json`.

## Windows day preferences (2026-10-22)

`DayPreferenceChecks.cs` covers existing-timetable updates with partial faculty participation, moving unconfigured faculty when necessary, soft preferred versus hard unavailable days, no forced attendance count, no inherited period restrictions, legacy profile conversion, generation before any timetable exists, later faculty submissions, stale/canceled saves, subgroup practical exchange, locks, and atomic rollback on solver rejection or an injected database write failure.

Windows UI checks now verify five day cards, no period matrix, automatic validated timetable updates on save, optional profile removal, visible actions and usable current-timetable height at 800x680 and 1180x820. Screenshots: `preferences-days-800.png`, `preferences-days-1180.png`, `preferences-days-updated.png` in the same verification output directory. The web checks continue to exercise the legacy matrix/preview/commit workflow for compatibility.

`SectionDayLimitChecks.cs` verifies a four-day maximum per section/semester with locked lectures, rejection and atomic rollback when hard faculty days would force a fifth day, different day sets across sections, individual faculty optimization against fixed lectures, and final rejection of a modified preview. `tests/RowNumbering` also verifies chronological day/time ordering across branches and retention of that ordering after filters are reapplied.
