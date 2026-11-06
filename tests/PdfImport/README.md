# PDF import verification

Run from the solution directory:

```powershell
dotnet run --project tests/PdfImport/PdfImport.csproj
```

Runs without a database. Creates deterministic PDF and Excel fixtures and checks multi-page extraction, source line numbers, blank cells, timetable detection, shaped Arabic text and headers, invalid input, cancellation and the shared Excel reader. The former desktop import form has been removed.

The SQL-backed PDF import round trip is part of `tests/ExcelImport/ExcelImportTests.csproj`; its README describes the isolated test databases.
