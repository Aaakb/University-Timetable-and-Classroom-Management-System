# Presentation/BLL refactoring checks

Run on Windows from the repository root:

```powershell
dotnet run --project tests/PresentationBllRefactoring -c Release
```

The 51 checks use no database. They exercise the shared editor action on a real
Windows Forms message loop, including UI-thread continuations, navigation while
save/reload is pending, synchronous/asynchronous failures, exception identity,
and action-state restoration. They also verify the seven services' record-ID
message formats and validation/query ordering through the shared BLL validator.

Pass `-- presentation-only` to run the nine editor checks alone. The executable
returns a nonzero exit code on failure and writes results to
`.tmp/PresentationBllRefactoring/action-checks.json`.

These checks complement the SQL-backed application/regression suites and the
runtime `DesignerCompatibility` and `DesignerLifecycle` harnesses. They do not
replace opening, saving and reopening forms in Visual Studio Designer.
