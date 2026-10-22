# Practical exchange regression checks

Run `dotnet run --project tests/PracticalExchange/PracticalExchange.csproj -c Debug` (or Release) from the repository root.

The suite runs 17 synthetic checks for the internal strict exchange mode and 4 SQL persistence checks for the application's flexible mode. The application prefers exchanges but permits separate group sessions when needed, including when both subjects share a teacher. SQL checks create a new database named UniversityTimetable_OriginalExchange_<random> using localhost and integrated security. Set UNIVERSITY_TIMETABLE_TEST_SERVER to use another test server. The process overrides UNIVERSITY_TIMETABLE_CONNECTION_STRING before database access and never opens the production database. Generated test databases are retained for inspection.

Results are written to .tmp/OriginalPracticalExchange/results/checks-debug.json or checks-release.json.
