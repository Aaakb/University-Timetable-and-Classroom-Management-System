# PDF export checks

From the repository root:

```powershell
dotnet run --project tests/SchedulePdfExport/SchedulePdfExport.csproj
python tests/SchedulePdfExport/verify.py
```

The C# probe exports fixtures and reproduces the six-lesson report for ورود حسن using read-only database queries. It uses the normal configured connection string. The Python verifier requires `pymupdf` and verifies the rendered PDFs: instructor grouping by semester, academic grouping for normal exports, complete Sunday–Thursday headers placed from right to left, correct lesson columns and periods, 24-hour times, and a readable empty report. A mismatched instructor selection must be rejected.

Intermediate PDFs and previews are under `tmp/pdfs/instructor-export`; the corrected instructor sample is under `output/pdf/instructor-warood-hassan.pdf`.
