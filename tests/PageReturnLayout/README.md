# Page return and resize layout regression

Run on Windows with .NET 8 or later:

```powershell
dotnet build tests/PageReturnLayout -c Release --artifacts-path .tmp/PageReturnLayout
dotnet .tmp/PageReturnLayout/bin/PageReturnLayout/release/PageReturnLayout.dll
```

The isolated output also allows checking the fix while the desktop application
is open, without overwriting its loaded DLLs.

The harness recreates and revisits eight navigation pages in a real WinForms
host. It narrows each page to 420, 800 and 1090 pixels, returns to its original
width, and repeats resizing while hidden. It requires the editor to return to
its original compact height and the results area to recover its original space.
Only the pages' data-loading OnLoad overrides are suppressed: their runtime
constructors, Arabic setup and responsive layout still run. No database is read
or written.

Before the fix, 72 of 128 checks failed. The subjects editor retained a height
of 620 pixels after widening, despite its content requiring only 253 pixels.
ResponsiveFlowPanel clamped an explicitly proposed width to its parent's old
narrow bounds during measurement. Honoring that proposed width fixes the
AutoSize row in the same pass. All 128 checks pass after the fix.
