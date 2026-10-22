# Arabic interfaces

The harness checks Arabic captions and headings on the 12 previously English
Windows Forms screens, RTL layout, Western digits (0–9), translated validation,
and Arabic combo/grid display while preserving canonical saved values. It also
captures representative screens. The default run uses designer mode and never
connects to the database.

```powershell
dotnet build 'University Timetable and Classroom Management System/Presentation_Layer.csproj' -c Release
dotnet run --project tests/ArabicInterfaces -c Release -- 'University Timetable and Classroom Management System/bin/Release/net8.0-windows/Presentation_Layer.dll' 'tmp/arabic-ui-qa'
```

Optionally pass an existing isolated `UniversityTimetable_FourFixes_*` database
name with the current application schema as the final argument to exercise actual sidebar navigation and read-only
data loading. This mode never submits save, delete, import or generation actions.
