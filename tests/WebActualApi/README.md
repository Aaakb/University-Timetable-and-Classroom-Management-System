# Web API and browser integration checks

Run from the repository root:

```powershell
dotnet run --project tests/WebActualApi/WebActualApi.csproj
```

The test creates a uniquely named disposable SQL Server database using the configured server, starts its own local API, and deletes that database in `finally`. It exercises sign-in, subject updates, Excel name import, and browser-based CRUD and timetable filtering. It requires SQL Server permissions to create and delete the test database, Node.js, Playwright, and Microsoft Edge. It does not use the university database.

If Playwright is installed elsewhere, set `PLAYWRIGHT_MODULE` to its `index.mjs` path.

The administrator is seeded outside the HTTP API in the disposable database. Checks include closed anonymous registration, a read-only user denied POST/PUT/DELETE/generation/registration, short new passwords rejected, SQL paging and search beyond the first page, and read-only browser controls.
