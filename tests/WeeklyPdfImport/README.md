# Supplied weekly PDF regression

```powershell
dotnet run --project tests/WeeklyPdfImport/WeeklyPdfImport.csproj -- "D:\Telegram Desktop\اجدول الدروس الاسبوعي  2026 - 2027 (2).pdf"
```

This test intentionally takes the user-supplied PDF as an external fixture; it is not copied into the repository. It checks the 17 pages and 235 source lectures, merged spans, Arabic PDF ligatures, branch/group metadata, 24-hour times, missing source values, and morning/evening separation. The real first page also passes validation, repair and atomic saving in a uniquely named `UniversityTimetable_WeeklyPdf_<guid>` SQL Server database.

The application's database is never selected. The test uses `localhost` with integrated authentication, or `UNIVERSITY_TIMETABLE_TEST_SERVER` when set. Test databases remain available for inspection. UI tests open the actual file at 800×600 and 1180×800 and verify scope filtering and action visibility. JSON results and screenshots are under `output/weekly-pdf-import-verification/`.
