# Designer compatibility regression

Run on Windows with .NET 8 and SQL Server on `localhost`. First run the database checks in `tests/TimetableRegression` to create an isolated database. This harness refuses database names outside the `UniversityTimetable_FourFixes_` prefix and overrides the application connection string before constructing a screen. It does not use the application's configured database or create user accounts.

From the repository root:

```powershell
dotnet build '.\University Timetable and Classroom Management System\Presentation_Layer.csproj' -c Release
dotnet build '.\tests\DesignerCompatibility\DesignerCompatibility.csproj' -c Release
$application = (Resolve-Path '.\University Timetable and Classroom Management System\bin\Release\net8.0-windows\Presentation_Layer.dll').Path
$database = (Get-Content '.\.tmp\FourFixes\results\database.txt').Trim()
& '.\tests\DesignerCompatibility\bin\Release\net8.0-windows\DesignerCompatibility.exe' $application '.\.tmp\DesignerCompatibility\final-ui' $database
```

The test opens the actual application screens and exits automatically. It writes `checks.json`, screenshots, and control/property manifests to the output directory. Its exit code is nonzero on a failed assertion or an unhandled UI exception.

Checks cover navigation through the actual sidebar button handlers, active navigation state, visible action bounds, preserved button handlers, explicit data-bound columns, contiguous visible row numbering, single-word column headers fitting their available widths, weekly rows/card selection/scrolling, preventing scrolled card text from painting over headers, disposing a selected schedule page, keyboard default buttons, and password mask/icon toggling. Screens are captured at shell widths of 1280 and 1600 pixels. The separate timetable regression harness checks the schedule and time-slot layouts down to the minimum window width. `tests/RowNumbering` tests gapped identities/filtering/sorting separately; `tests/DesignerLifecycle` reproduces and verifies the Guna label teardown fix.

This runtime test does not automate or replace Visual Studio Design View. The actual edit/save/close/reopen results for all 12 screens are recorded in `docs/DESIGNER_COMPATIBILITY.md`. It also does not submit Add, Update, Delete, Generate, Sign In, or Create Account actions; the existing database regression checks exercise timetable mutations separately.

Add `review` as a fourth argument to open the teaching assignments page without running the assertion loop. This mode leaves the window open for native visual inspection. Close it when finished; bitmap captures alone can miss an individual Guna repaint.

Add `displays` as a fourth argument to check all nine pages and both account forms at equivalent available viewports for 1280×720, 1366×768, 1600×900, 1920×1080, and 2560×1440, each at 100%, 125%, 150%, and 200% zoom. This produces 680 assertions covering window bounds, scroll access to fields/grids/actions/sidebar commands, and overlapping input/action controls. The test scrolls every ancestor viewport and checks the complete button rectangle; for wide fields and grids it checks access to both ends, rather than requiring the entire control to be visible at once. Add `small` as a fifth argument to capture only the smallest equivalent viewport (34 assertions).

The display test resizes the actual forms to simulate available space. It does not change Windows display settings or simulate native DPI-change messages, font rasterization, or physically moving between monitors. The tested device has one physical monitor at 96 DPI. Runtime DPI support is configured as `PerMonitorV2`, and custom layout dimensions use the current control DPI. A test on two physical monitors with different DPI settings remains separate.

The regular 160-check run also verifies the 08:30–09:50 initial time-slot defaults, the visible flexible-duration hint, minute-precision conversion of an edited 90-minute afternoon period without submitting database writes, and 18 cases where a page that fits its host must not leave unnecessary outer scrollbars after navigation.
