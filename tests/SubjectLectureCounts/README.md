# Editable weekly lecture counts

```powershell
dotnet run --project tests/SubjectLectureCounts -c Release
node --experimental-vm-modules tests/SubjectLectureCounts/web-check.mjs
```

The Windows harness creates its own `UniversityTimetable_LectureCounts_*`
database. It verifies schema upgrades, persistence, API mappings and validation,
actual timetable generation with repeated theory and practical occurrences,
protection of saved lectures when reducing counts, compatibility with older
imports, and the real subject editor's automatic/manual/reset behavior.
It also verifies count-only editing, deletion while preserving assignments and
saved lectures, exclusion from new generation, and safe reuse of a subject name.
It never changes the application's configured database. The screenshot is saved
under `tmp/lecture-count-qa`.
