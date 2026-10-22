# Excel import verification

Run from the solution directory:

```powershell
dotnet run --project tests/ExcelImport/ExcelImportTests.csproj
dotnet run --project tests/ExcelImport/ExcelImportTests.csproj --no-build -- ui
```

The SQL tests create uniquely named `UniversityTimetable_ExcelImport_<guid>` and
`UniversityTimetable_ExcelImport_<guid>_Legacy` databases on `localhost`. Set
`UNIVERSITY_TIMETABLE_TEST_SERVER` to use another SQL Server with integrated authentication.
The application database is never used. Test databases remain available for inspection.

The suite covers workbook parsing, templates, Arabic values and column mapping,
atomic academic importing, explicit updates, ambiguous references, all three conflict
types, room suitability and capacity, weekly requirement limits, idempotent importing,
hybrid practical group completion, persistent locks, ordinary generation, repair search
invariants, database changes after preview, rollback on failure and cancellation, and
the additive legacy schema upgrade.

`ui` runs in STA mode, opens the import form with fixture data in each mode at
800×600 and 1180×800, checks that action buttons fit, verifies the hybrid checkbox
column, and saves screenshots. It does not open or write to a database.

Results, examples, rendered templates and screenshots are in
`output/excel-import-verification/`. The three clean distributable templates are in
`Templates/Excel/`.
