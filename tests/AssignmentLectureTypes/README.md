# Teaching assignment components

Run `dotnet run --project tests/AssignmentLectureTypes/AssignmentLectureTypes.csproj`.

The suite uses an isolated SQL Server database and verifies legacy schema upgrade,
component-specific generation, manual/API validation, assignment edits, Excel
compatibility, hybrid generation, alternative plans, regeneration and backups.
It does not alter the application's production database.
