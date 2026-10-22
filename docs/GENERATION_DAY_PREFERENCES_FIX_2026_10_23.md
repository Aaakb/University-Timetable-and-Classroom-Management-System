# Complete regeneration with strict faculty day preferences

The fast placement pass could stop with eight unplaced lectures out of 263 after
all 50 faculty day profiles were entered. `ScheduleService` rejected the partial
plan before its constraint solver could consider the missing lectures.

Fresh regeneration now retries an incomplete fast plan with every remaining
request in a complete constraint model. It solves the two semesters independently,
keeps explicit locks fixed, filters faculty by their theory/practical assignments,
and preserves the configured rooms, capacities, periods and strict availability.
The original fast-placement and incremental-preservation paths remain in use.

No partial plan is saved. A proven infeasible model and an exhausted search budget
receive different errors, and both preserve the saved timetable and preferences.
Successful regeneration still takes an automatic backup before replacing lectures.

Validation uses an isolated backup/restore copy containing all 263 lectures, all
50 day profiles and 4 halls plus 7 labs, including 3 shared labs. It covers two
complete regenerations, locks in both semesters, weekly counts, all lecture
service validations, unchanged academic resources/preferences, incremental no-op
behavior and failure rollback for an impossible one-day workload. Additional
assignment-component and timetable regression suites passed.

```powershell
dotnet run --project tests/FullGenerationPreferences/FullGenerationPreferences.csproj -c Release -- UniversityTimetable_GenerationPrefs_20261023_v2
```
