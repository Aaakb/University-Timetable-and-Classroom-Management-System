using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly string[] RequiredTables = ["Subjects", "FacultyMembers", "Schedules", "ApplicationUsers"];

    private static async Task<int> Main(string[] args)
    {
        StreamWriter? log = null;
        try
        {
            if (args.Length == 0) throw new ArgumentException("Choose backup, prepare, or compare.");
            var options = Parse(args.Skip(1).ToArray());
            if (options.TryGetValue("--log", out string? logPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath))!);
                log = new StreamWriter(logPath, append: false) { AutoFlush = true };
                Console.SetOut(log);
                Console.SetError(log);
            }
            switch (args[0])
            {
                case "backup": await BackupAsync(options); break;
                case "prepare": await PrepareAsync(options); break;
                case "compare": await CompareAsync(options); break;
                default: throw new ArgumentException("Unknown database setup command.");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Database setup failed: " + ex.Message);
            return 1;
        }
        finally { log?.Dispose(); }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || !args[i].StartsWith("--") || !result.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Invalid or duplicate setup option.");
        }
        return result;
    }

    private static string Need(Dictionary<string, string> args, string name) =>
        args.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException("Missing option: " + name);
    private static string Identifier(string value) => "[" + value.Replace("]", "]]") + "]";
    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    private static string LocalName(string value)
    {
        if (!Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9_]{0,95}$"))
            throw new ArgumentException("The local instance/database name is invalid.");
        return value;
    }

    private static string ReadConnection(string settings)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(settings)));
        return doc.RootElement.GetProperty("ConnectionStrings").GetProperty("UniversityTimetable").GetString()
            ?? throw new InvalidOperationException("The database connection is empty.");
    }

    private static SqlCommand Command(SqlConnection connection, string sql, int seconds = 120)
        => new(sql, connection) { CommandTimeout = seconds };
    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        using var command = Command(connection, sql);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task BackupAsync(Dictionary<string, string> args)
    {
        string destination = Path.GetFullPath(Need(args, "--output"));
        if (File.Exists(destination)) throw new IOException("The destination backup already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string source = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_CONNECTION_STRING")
            ?? ReadConnection(Need(args, "--source-settings"));
        var builder = new SqlConnectionStringBuilder(source);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) ||
            new[] { "master", "model", "msdb", "tempdb" }.Contains(builder.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("A user database is required.");
        await using var connection = new SqlConnection(source);
        await connection.OpenAsync();
        string version;
        string backupDirectory;
        using (var metadata = Command(connection, "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')), CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))"))
        await using (var reader = await metadata.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            version = reader.GetString(0);
            backupDirectory = reader.GetString(1);
        }
        if (int.Parse(version.Split('.')[0]) > 17) throw new InvalidOperationException("This package requires a newer LocalDB engine.");
        string serverBackup = Path.Combine(backupDirectory, "UniversityTimetable_Installer_" + Guid.NewGuid().ToString("N") + ".bak");
        var counts = await CountsAsync(connection);
        try
        {
            // COPY_ONLY preserves the source database's normal backup chain. No detach or data updates.
            using (var backup = Command(connection,
                $"BACKUP DATABASE {Identifier(builder.InitialCatalog)} TO DISK=@file WITH COPY_ONLY, CHECKSUM, INIT", 600))
            {
                backup.Parameters.AddWithValue("@file", serverBackup);
                await backup.ExecuteNonQueryAsync();
            }
            using (var verify = Command(connection, "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM", 600))
            {
                verify.Parameters.AddWithValue("@file", serverBackup);
                await verify.ExecuteNonQueryAsync();
            }
            File.Copy(serverBackup, destination, overwrite: false);
            string hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(destination)));
            var manifest = new Snapshot(DateTimeOffset.UtcNow, builder.InitialCatalog, version, hash, counts);
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(destination)!, "Snapshot.json"), JsonSerializer.Serialize(manifest, Json));
            Console.WriteLine($"Verified COPY_ONLY backup: {counts.Count} tables; SQL {version}; {new FileInfo(destination).Length} bytes.");
        }
        finally
        {
            // This exact file was uniquely created above; never remove a pre-existing source backup.
            if (File.Exists(serverBackup)) File.Delete(serverBackup);
        }
    }

    private static async Task PrepareAsync(Dictionary<string, string> args)
    {
        string backup = Path.GetFullPath(Need(args, "--backup"));
        string dataDirectory = Path.GetFullPath(Need(args, "--data-dir"));
        string settings = Path.GetFullPath(Need(args, "--settings"));
        string instance = LocalName(args.GetValueOrDefault("--instance", "UniversityTimetable"));
        string database = LocalName(args.GetValueOrDefault("--database", "UniversityTimetable"));
        string localDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Microsoft SQL Server", "170", "Tools", "Binn", "SqlLocalDB.exe");
        if (!File.Exists(localDb)) throw new FileNotFoundException("Install Microsoft SQL Server 2025 LocalDB first.", localDb);
        if (!File.Exists(backup)) throw new FileNotFoundException("The packaged database backup is missing.", backup);
        var manifest = JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(backup)!, "Snapshot.json")))
            ?? throw new InvalidDataException("The database snapshot manifest is invalid.");
        string hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(backup)));
        if (hash != manifest.Sha256) throw new InvalidDataException("The packaged database checksum does not match.");
        // SqlLocalDB can return exit code 0 even when `info <missing-name>` fails.
        // The argument-free listing provides exact instance names without localized labels.
        async Task<bool> InstanceExistsAsync()
        {
            string listing = "";
            await RunAsync(localDb, ["info"], text => listing = text);
            return listing.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(instance, StringComparer.OrdinalIgnoreCase);
        }
        if (!await InstanceExistsAsync())
        {
            await RunAsync(localDb, ["create", instance, "17.0"]);
            if (!await InstanceExistsAsync()) throw new InvalidOperationException("LocalDB did not create the requested application instance.");
        }
        await RunAsync(localDb, ["start", instance]);
        var master = new SqlConnectionStringBuilder
        {
            DataSource = "(localdb)\\" + instance, InitialCatalog = "master", IntegratedSecurity = true,
            Encrypt = false, TrustServerCertificate = true, ConnectTimeout = 5
        };
        await using var connection = new SqlConnection(master.ConnectionString);
        await OpenWithRetryAsync(connection);
        using (var version = Command(connection, "SELECT CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))"))
            if (Convert.ToInt32(await version.ExecuteScalarAsync()) < int.Parse(manifest.SqlVersion.Split('.')[0]))
                throw new InvalidOperationException("The local database engine is older than the packaged database.");
        bool exists;
        using (var check = Command(connection, "SELECT DB_ID(@database)"))
        {
            check.Parameters.AddWithValue("@database", database);
            exists = await check.ExecuteScalarAsync() is not DBNull;
        }
        if (!exists)
        {
            Directory.CreateDirectory(dataDirectory);
            var moves = new List<string>();
            using (var files = Command(connection, "RESTORE FILELISTONLY FROM DISK=@file"))
            {
                files.Parameters.AddWithValue("@file", backup);
                await using var reader = await files.ExecuteReaderAsync();
                int ordinal = 0;
                while (await reader.ReadAsync())
                {
                    string type = reader.GetString(reader.GetOrdinal("Type"));
                    if (type is not ("D" or "L")) throw new NotSupportedException("The database contains unsupported FILESTREAM or special files.");
                    string extension = type == "L" ? ".ldf" : ordinal == 0 ? ".mdf" : ".ndf";
                    string path = Path.Combine(dataDirectory, database + "_" + ordinal++ + extension);
                    if (File.Exists(path)) throw new IOException("An existing database file will not be overwritten: " + path);
                    moves.Add("MOVE " + Literal(reader.GetString(reader.GetOrdinal("LogicalName"))) + " TO " + Literal(path));
                }
            }
            if (moves.Count == 0) throw new InvalidDataException("The database backup contains no files.");
            // Deliberately no WITH REPLACE: installation must never replace an existing database.
            using var restore = Command(connection,
                $"RESTORE DATABASE {Identifier(database)} FROM DISK=@file WITH CHECKSUM, RECOVERY, " + string.Join(", ", moves), 600);
            restore.Parameters.AddWithValue("@file", backup);
            await restore.ExecuteNonQueryAsync();
            using var ownerQuery = Command(connection, "SELECT SUSER_SNAME()");
            string owner = Convert.ToString(await ownerQuery.ExecuteScalarAsync())
                ?? throw new InvalidOperationException("Unable to determine the local database owner.");
            await ExecuteAsync(connection, $"ALTER AUTHORIZATION ON DATABASE::{Identifier(database)} TO {Identifier(owner)}");
            Console.WriteLine("Restored the packaged database into a new local database.");
        }
        else Console.WriteLine("Preserved the existing local database; no restore performed.");
        master.InitialCatalog = database;
        await using var installed = new SqlConnection(master.ConnectionString);
        await installed.OpenAsync();
        foreach (string table in RequiredTables)
        {
            using var check = Command(installed, "SELECT OBJECT_ID(@name, 'U')");
            check.Parameters.AddWithValue("@name", "dbo." + table);
            if (await check.ExecuteScalarAsync() is DBNull) throw new InvalidDataException("The existing database is not compatible: missing " + table);
        }
        if (!exists) await ExecuteAsync(installed, "DBCC CHECKDB WITH NO_INFOMSGS");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        string temporary = settings + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new { ConnectionStrings = new { UniversityTimetable = master.ConnectionString } }, Json));
            File.Move(temporary, settings, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine("Database connection settings written. User database files are preserved on uninstall.");
        if (args.TryGetValue("--application", out string? application))
            await RunAsync(Path.GetFullPath(application), ["--update-db"]);
    }

    private static async Task CompareAsync(Dictionary<string, string> args)
    {
        var expected = JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(Need(args, "--manifest")))
            ?? throw new InvalidDataException("Invalid snapshot manifest.");
        await using var connection = new SqlConnection(ReadConnection(Need(args, "--settings")));
        await connection.OpenAsync();
        var actual = await CountsAsync(connection);
        foreach (var row in expected.Tables)
            if (!actual.TryGetValue(row.Key, out long count) || count != row.Value)
                throw new InvalidDataException($"Restored table count mismatch: {row.Key}.");
        Console.WriteLine($"Verified row counts for all {expected.Tables.Count} packaged tables.");
    }

    private static async Task<SortedDictionary<string, long>> CountsAsync(SqlConnection connection)
    {
        var tables = new List<(string Schema, string Name)>();
        using (var query = Command(connection, "SELECT SCHEMA_NAME(schema_id),name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY schema_id,name"))
        await using (var reader = await query.ExecuteReaderAsync())
            while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            using var count = Command(connection, $"SELECT COUNT_BIG(*) FROM {Identifier(table.Schema)}.{Identifier(table.Name)}");
            result[table.Schema + "." + table.Name] = Convert.ToInt64(await count.ExecuteScalarAsync());
        }
        return result;
    }

    private static async Task OpenWithRetryAsync(SqlConnection connection)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { await connection.OpenAsync(); return; }
            catch (SqlException) when (attempt < 5) { await Task.Delay(1000); }
        }
    }

    private static async Task<int> RunAsync(string executable, string[] args, Action<string>? outputHandler = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Remove("UNIVERSITY_TIMETABLE_CONNECTION_STRING");
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Unable to start LocalDB.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        string stdout = await output;
        outputHandler?.Invoke(stdout);
        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout.Trim());
        if (process.ExitCode != 0) throw new InvalidOperationException("Setup process failed: " + await error);
        return process.ExitCode;
    }

    private sealed record Snapshot(DateTimeOffset CreatedAtUtc, string SourceDatabase, string SqlVersion,
        string Sha256, SortedDictionary<string, long> Tables);
}
