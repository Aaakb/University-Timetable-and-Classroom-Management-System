# Embedded preferences and alternatives navigation

Run on Windows with SQL Server and .NET 8 or later:

```powershell
dotnet run --project tests/EmbeddedPageNavigation -c Release --artifacts-path C:/CodexBuilds/EmbeddedPageNavigation
```

The server defaults to `localhost`; override it with
`UNIVERSITY_TIMETABLE_TEST_SERVER`. Use a short artifacts path so SqlClient's
native library can be loaded on Windows. The test creates a randomly named
`UniversityTimetable_EmbeddedNavigation_*` database and removes that exact
database on completion. It does not connect to the configured application DB.

This runs the actual main window and navigation events with a hidden test shell.
It checks that preferences and timetable alternatives are visible children of
the shell, keep the sidebar, load their data, and return to the previous page.
It saves a change through the preferences UI and checks the isolated database;
it also checks that a canceled close or a busy operation prevents navigation
and that a busy operation receives a cancellation request. No confirmation
dialog is displayed: a FormClosing cancellation is injected directly. The
yes/no buttons of the unsaved-changes confirmation are therefore outside this
test's coverage.
