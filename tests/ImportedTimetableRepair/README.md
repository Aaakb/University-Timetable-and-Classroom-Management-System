# Imported timetable repair verification

```powershell
dotnet run --project tests/ImportedTimetableRepair/ImportedTimetableRepair.csproj
```

Creates a uniquely named `UniversityTimetable_ImportRepair_<guid>` database on `localhost`, using integrated authentication. Set `UNIVERSITY_TIMETABLE_TEST_SERVER` for another test server. Never uses the application's configured database. The isolated database remains available for inspection.

Covers room, faculty and section conflicts; fixed rows and saved locks; unsuitable rooms; stable IDs and unrelated saved rows; idempotent matching; invalid identities and excess weekly requirements; practical exchange; no-solution cases; stale/tampered previews; atomic rollback and cancellation; actual Arabic Excel and multi-page text PDF imports; Arabic normalization and ambiguous names. Results and fixtures are written to `output/import-repair-verification/`.

`tests/PdfImport/PdfImport.csproj` covers the Arabic UI at 800×600 and 1180×800 in all four modes, including repair warnings, locks, save gating and before/after comparison.
