# Scheduling capacity probe

Run `dotnet run --project tests/SchedulingCapacity/SchedulingCapacity.csproj -c Release --artifacts-path D:/CodexCapacity20261015` from the repository root.

The harness makes a complete SQL Server copy-only backup/restore of the configured application database, then exports all copied subjects, assignments, sections, rooms and lecture periods. Every scenario uses a fresh in-memory request list and the actual production planner in flexible mode. Stress subjects and faculty are never saved to the original database or the copy. The baseline is 96 subjects and 293 required bookings across both semesters.

Subjects are added to the third/fourth-year branch scopes with the lowest current student timetable loads. Each subject has one theory lesson, two practical group lessons, or both depending on scenario. The existing-staff scenario assigns the faculty candidates of the first existing subject in the same year/branch/semester. The dedicated-staff scenario provides one new simulated teacher for each extra subject.

Each sequence stops at the first failed complete plan or a 30-second worker timeout. These are observed results for these insertion orders and allocations, not a proof of globally optimal capacity. Successful plans are checked for exact lesson coverage, faculty eligibility, student capacity, and booking conflicts. Current records all have nonzero theoretical hours; the probe's coverage check assumes that fixture.

Results: `.tmp/SchedulingCapacity/results.json`, full copied input: `fixture.json`, and retained isolated database: `database.txt`. The two-semester combined probe uses:

```powershell
dotnet run --project tests/SchedulingCapacity/SchedulingCapacity.csproj -c Release --artifacts-path D:/CodexCapacity20261015-pair -- worker 1 Mixed Existing 4 3 Dedicated
```

That probe verifies 4 extra mixed subjects in semester 1 and 3 in semester 2, with new simulated teachers for the latter. The successful production plan contained all 314 required lessons and took about 14 seconds on the test machine. Its exact result is recorded in `combined-result.json`.
