# Full timetable generation with strict day profiles

Supply a SQL backup/restore copy of the complete application database, with all
50 day profiles and the 4 halls plus 7 labs configuration. The test rejects the
production database name.

```powershell
dotnet run --project tests/FullGenerationPreferences/FullGenerationPreferences.csproj -c Release -- UniversityTimetable_GenerationPrefs_20261023
```

Checks two complete 263-lecture regenerations, weekly requirements, immutable
academic resources and preferences, every lecture's service validation, automatic
backup, incremental no-op behavior and rollback for an impossible workload.
