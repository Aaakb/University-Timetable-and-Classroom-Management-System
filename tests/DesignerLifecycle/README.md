# Designer label lifecycle regression

Run on Windows after building the application:

```powershell
dotnet build 'University Timetable and Classroom Management System/Presentation_Layer.csproj' -c Release
dotnet build tests/DesignerLifecycle/DesignerLifecycle.csproj -c Release
dotnet tests/DesignerLifecycle/bin/Release/net8.0-windows/DesignerLifecycle.dll 'University Timetable and Classroom Management System/bin/Release/net8.0-windows/Presentation_Layer.dll' .tmp/DesignerDiagnosis/lifecycle-checks.json
```

The test reproduces Guna's four null-reference failures after an unwrapped HTML label is disposed, then verifies the compatibility label through the same `TypeDescriptor` property access used by a Properties window. It checks default metadata, editable live values, requests during and after disposal, repeated disposal, and all 120 labels currently present across the 12 application screens. A design-time `LicenseContext` suppresses application event wiring and data loading; no database or application window is opened.

This lifecycle test complements an actual Visual Studio edit/save/close/reopen check; it does not replace that check.
