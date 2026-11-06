# Practical exchange during fresh generation

Run `dotnet run --project tests/PracticalExchangeGeneration/PracticalExchangeGeneration.csproj` from the repository root. Use a short `--artifacts-path` if Windows native dependency paths are too long. The test reads the configured SQL Server connection and creates a uniquely named `UniversityTimetable_ExchangeTest_<guid>` database, which it removes in `finally`. It never modifies the configured application database.

The fallback regression starts from a feasible seed with subgroup sessions on separate days. Fresh generation must reorganize it into a same-day exchange, retain semester one, keep explicitly merged practicals whole, maintain old backup compatibility only when grouping is unchanged, and roll back if exchange constraints cannot be met.
