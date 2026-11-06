# Delete a teaching assignment and its timetable rows

```powershell
dotnet run --project tests/AssignmentDeletion/AssignmentDeletion.csproj --artifacts-path output/AssignmentDeletionBuild
```

Uses a randomly named `UniversityTimetable_AssignmentDeletion_*` SQL Server
database on the configured instance. The production database is not selected.
The test database is removed in `finally` after checking its exact generated name.

The 21 SQL-backed checks cover:

- Deleting active and archived-subject assignments with scheduled lectures.
- Removing theory/practical placements, multiple covered sections, and locked rows.
- Preserving the subject's values and visibility, teachers, other assignments,
  and other teachers'/subjects'/semesters' placements.
- Reassigning the retained subject and deleting an assignment without lectures.
- Missing-assignment handling without timetable changes.
- Atomic rollback: a temporary foreign key intentionally rejects assignment
  deletion after the timetable DELETE has run. Both the assignment and the
  exact timetable rows must remain when the transaction rolls back.
- Separately deleting the retained subject from its material list afterward.

The desktop still presents a confirmation before deletion. It explicitly warns
that associated lectures, including locked lectures, will be removed.

The existing SubjectLectureCounts suite now checks that assignment deletion
removes archived-subject lectures too. Subject deletion now removes all of the
subject's assignments and lectures. The suite deliberately seeds a separate
legacy-history fixture so its preexisting archive editing/regeneration/backup
regressions continue to run; that fixture is not a side effect of deletion.

Run the focused archive/deletion checks with:

```powershell
dotnet build tests/SubjectLectureCounts/SubjectLectureCounts.csproj -c Release --artifacts-path output/AssignmentDeletionBuild
dotnet output/AssignmentDeletionBuild/bin/SubjectLectureCounts/release/SubjectLectureCounts.dll --subject-deletion-only
```

This explicit focused mode sets up the real generator/database fixture and runs
38 deletion/archive/backup checks. The default full run still includes the
independent Excel import assertion; it is not disabled or relaxed.
