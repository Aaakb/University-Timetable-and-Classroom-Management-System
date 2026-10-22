# PDF import verification

Run from the solution directory:

```powershell
dotnet run --project tests/PdfImport/PdfImport.csproj
```

Runs without a database. Creates deterministic PDF and Excel fixtures and checks multi-page extraction, source line numbers, blank cells, timetable detection, shaped Arabic text and headers, invalid input, cancellation and the shared Excel reader. Opens the Arabic import form in all four modes at 800×600 and 1180×800, checks mappings, repair warnings, locks, save gating, comparison layout and action visibility, and saves screenshots under `output/pdf-import-verification/`.

The SQL-backed PDF import round trip is part of `tests/ExcelImport/ExcelImportTests.csproj`; its README describes the isolated test databases.
