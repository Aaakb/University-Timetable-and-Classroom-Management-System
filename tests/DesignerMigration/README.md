# WinForms Designer migration regression

This Windows/.NET 8 harness captures the four formerly code-only dialogs before
their UI is moved into `*.Designer.cs`, then compares the migrated dialogs with
that baseline. It never shows an application form, submits an action, changes
selections for the snapshot, raises the application's `Shown`, or connects to a
database. To capture controls whose invisible parent would otherwise suppress
painting, it temporarily hosts their UI root inside a neutral transparent form
offscreen. Application `Load`/`Shown` handlers are never invoked.

```powershell
dotnet build tests/DesignerMigration -c Release
dotnet tests/DesignerMigration/bin/Release/net8.0-windows/DesignerMigration.dll capture 'University Timetable and Classroom Management System/bin/Release/net8.0-windows/Presentation_Layer.dll' '.tmp/DesignerMigration20261024/baseline'
# Rebuild the application after migrating one dialog, then:
dotnet tests/DesignerMigration/bin/Release/net8.0-windows/DesignerMigration.dll verify 'University Timetable and Classroom Management System/bin/Release/net8.0-windows/Presentation_Layer.dll' '.tmp/DesignerMigration20261024/after' '.tmp/DesignerMigration20261024/baseline' ExcelImportForm
```

Omit the final form name to check all four dialogs. Keep each verification output
directory separate when recording a sequence of migrations. Do not overwrite
the baseline after production changes.

Snapshots record each control by its original traversal path, with its original
field identities and control name for review. Comparisons cover control type and
order, text, bounds, fonts, colors, padding/margins, docking/anchoring, visible and
enabled state, numeric limits/defaults, list choices, tab pages, table cells and
row/column styles, grids, columns and cell styles. Existing control names must be
preserved; adding names and fields to previously anonymous controls is allowed.
Snapshots are compared after the neutral host settles normal WinForms layout;
`*-initial.json` additionally preserves the unshown constructor state for diagnosis.
Both computed table cells and the explicit/automatic row/column assignments are
recorded. The sole display exception is the designer's canonical `StudyYears`
entity text versus the runtime Format event's `المراحل الدراسية`; selected index,
canonical item collection and runtime translated display remain checked.
Three Excel field wrappers use a DPI-scalable minimum height of 38, three faculty
editor wrappers use 34, and two preferences wrappers use 58 to retain their
original visible geometry after serialization; these exact property changes are
accepted only for those eight controls, and every original bound is still
compared exactly. The preferences dialog's five teaching-day cards are the only
added subtree accepted: they formerly appeared after asynchronous loading and
are now editable in the designer. Separate checks require five canonical day
names, Arabic captions, all three availability levels and canonical runtime
day/value mappings. The runtime probe invokes only the extracted in-memory day
choice binder, preserving its original timing after the asynchronous lookup in
production without invoking that lookup or any database service. Existing
controls outside that subtree remain exact.
Runtime checks also detect removed event subscriptions. Design-context checks
require `InitializeComponent`, no database service instance and no application
event handlers, so merely constructing a dialog cannot trigger its runtime
loading or save logic in the Visual Studio designer.

The Excel dialog additionally loads in-memory academic and timetable fixtures in
each of its four modes, exercising the real selection and mapping events. It
checks canonical schema identifiers, source row numbers, mapped values, workflow
visibility and enabled states, editable canonical `TRUE`/`FALSE` locks, and
duplicate-mapping rejection. These probes do not call the import service.

After the three navigation buttons are moved to the shell's Designer, a full
`verify` run also checks their presence, unique names and rows, footer placement,
and absence of runtime page loading in design mode. Pass `MainShellForm` as the
last argument to run only those shell checks. The shell is never instantiated in
runtime mode because its constructor loads the first page's data.

Each run writes JSON manifests, PNGs, exact property differences, and
`checks.json`. Nonzero exit means a failed check. These checks complement actual
Visual Studio open/edit/save/close/reopen verification; they do not replace it.
Runtime data loading, file imports, optimization and database writes belong to
the application's existing integration/regression harnesses.

The recorded migration check on 24 October 2026 passed **124 checks with zero
failures**, process exit 0. It used the untouched application assembly saved in
`.tmp/DesignerMigration20261024/original-app` to create
`.tmp/DesignerMigration20261024/baseline-settled-v2`, then verified the final
Release application with:

```powershell
dotnet tests/DesignerMigration/bin/Release/net8.0-windows/DesignerMigration.dll verify 'University Timetable and Classroom Management System/bin/Release/net8.0-windows/Presentation_Layer.dll' '.tmp/DesignerMigration20261024/final' '.tmp/DesignerMigration20261024/baseline-settled-v2'
```

The summary, full manifests, empty exact-difference reports and rendered images
are in that `final` directory; console output is
`.tmp/DesignerMigration20261024/final.log`. These local artifacts supplement the
tracked test source and are not database backups or proof of an actual Visual
Studio designer edit/save/reopen cycle.
