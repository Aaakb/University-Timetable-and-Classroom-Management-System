# Row number regression checks

The record grids show a contiguous `No.` column starting at 1 for the current view.
This number is independent of the permanent database identity. The original bound
key columns remain hidden, and editor/update/delete paths continue to use real keys.
Read-only editor fields are labeled `Record ID` to make this distinction explicit.

Run on Windows with the .NET 8 SDK:

```powershell
dotnet build tests/RowNumbering/RowNumbering.csproj -c Release --artifacts-path .tmp/RowNumbering/build
& .tmp/RowNumbering/build/bin/RowNumbering/release/RowNumbering.exe
```

The checks construct the nine actual record pages in a design-time license context
and explicitly configure their runtime grids. They do not show forms, run OnLoad
database operations, or submit Add/Update/Delete actions. Fixtures use deliberately
noncontiguous keys (31, 104, and 208).

Coverage includes static Designer columns, unchanged hidden identities, contiguous
numbering after filtering, sorting, reload, insertion/deletion, empty results and
row visibility changes, repeated helper attachment, and the real Branch/Subject
editor identity paths. This complements the database-backed runtime checks in
`tests/DesignerCompatibility`; it does not replace actual Visual Studio Designer
verification.
