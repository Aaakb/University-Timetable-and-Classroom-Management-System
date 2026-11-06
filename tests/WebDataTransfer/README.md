# Isolated web transfer checks

Run from the repository root:

```powershell
dotnet run --project tests/WebDataTransfer
```

Uses the configured SQL server with a uniquely named disposable database, seeds synthetic resources, and starts its own API on a free loopback port. Exercises actual restore, safety backup undo, stale preview and account isolation, optional day preferences with timetable optimization, confirmed Excel name import, rejection of PDF and unsupported timetable modes, administrator-only registration and read-only account enforcement. The API subprocess and disposable database are removed in `finally`; the university database is never selected. Requires permission to create/drop this temporary database on the configured server.
