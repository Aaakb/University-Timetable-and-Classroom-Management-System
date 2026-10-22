# Timetable regression checks

Run from the repository root on Windows with .NET 8 and a local SQL Server instance. These checks create a new `UniversityTimetable_FourFixes_<random>` database, refuse to reuse an existing name, and do not read or modify the application's configured database. The test database is retained for inspection.

```powershell
# Set only if the test SQL Server is not localhost:
$env:UNIVERSITY_TIMETABLE_TEST_SERVER = '.\SQLEXPRESS'
dotnet run --project .\tests\TimetableRegression\TimetableRegression.csproj
dotnet run --project .\tests\TimetableRegression\TimetableRegression.csproj -- ui
dotnet run --project .\tests\TimetableRegression\TimetableRegression.csproj -- ui auto-close
dotnet run --project .\tests\TimetableRegression\TimetableRegression.csproj -- navigation
dotnet run --project .\tests\TimetableRegression\TimetableRegression.csproj -- timing
```

The first command verifies empty generation protection, normal generation/regeneration, successful and conflicting semester changes, preserved lesson IDs, parallel practical groups, and English/Arabic/mixed/empty/multi-page PDF export. It also verifies flexible 60/90/120-minute periods, rollback of conflicting edits, minimum gaps, startup preservation, generation from a custom-only calendar, safe rejection of legacy overlapping calendars, and preservation of deleted defaults. An additional isolated database with an Identity TimeSlots table covers atomic add/update on the original application's table type. Results and PDF samples are written under `.tmp/FourFixes/results`; both test databases are retained.

The `ui` command uses the most recently created test database, checks the schedule and time-slot actions at requested shell widths of 1100, 1280, 1600, 1920, and 1280 pixels, then returns to 1280 pixels for visual review. It records initial visibility separately and verifies that every tested action and filter is reachable in full after scrolling its ancestor viewports. The shell now permits smaller windows while retaining a readable page canvas. It leaves the test window open; close it after inspection.

Add `auto-close` to finish automatically after the final page loads. UI exceptions are recorded as failures, results are written after the message loop ends, and the process returns a nonzero exit code on failure.

The `navigation` command deliberately delays service reads using read-only table locks in the isolated database. It navigates through all nine pages while reads are pending, verifies that old pages are disposed, releases the locks, and awaits their load tasks. It detects disposed-control accesses even if they are caught, as well as unhandled UI exceptions, and verifies that the current subject page still loads its records. It exits automatically and writes 21 checks to `navigation-checks.json`; it does not change data or use the original database.

For PDF structure/text checks, install `pypdf` into your Python environment and run `python tests/TimetableRegression/verify_pdf.py`. Also render samples with Poppler and inspect the Arabic and mixed text visually; text extraction alone does not establish correct shaping or layout.

The `timing` command requires no database. Its 17 checks retain the three initial periods, accept varied durations and afternoon times, verify the initial default and explanatory message, and reject reversed/equal ranges, negative/next-day times, and non-minute service inputs.
