# Lower list filters on desktop screens

Windows-only, in-memory UI regressions. No database is read or written.
The real SubjectsForm and FacultyMemberSubjectsForm controls run in a design-time
license context, suppressing their database OnLoad operations.

```powershell
dotnet build tests/SemesterNavigation/SemesterNavigation.csproj -c Release --artifacts-path output/LowerFiltersBuild
dotnet output/LowerFiltersBuild/bin/SemesterNavigation/release/SemesterNavigation.dll
```

The 30 checks cover:

- Absence of the duplicate top semester selector on both screens.
- Placement of semester, stage and department filters in the lower list header.
- Intersection of all three filters, all-values selections, and empty results.
- Continued access to unclassified materials and archived assignments.
- Clearing incompatible department filters for general study years.
- Preserving stage/department selections when assignment lookups reload.
- Subject choices for assignment entry following semester and editor scope.
- Disabling list filters during save, preserving IDs, and clearing stale edit keys.
- Selecting a row without changing list filters or hiding other rows.
- Last specific semester shared through the actual navigation factory.

Screenshots of synthetic fixtures are saved in output/lower-filters. They do not
represent production database save workflows or Visual Studio Designer round trips.

Related checks: UiOpening and ArabicInterfaces. The SQL-backed AssignmentScopes
suite's UI section uses the lower semester combo; its independent Excel import
failure noted in earlier reports is outside these in-memory UI checks.
