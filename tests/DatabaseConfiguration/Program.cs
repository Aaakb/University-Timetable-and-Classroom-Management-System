using System.Text.Json;
using Data_Access_Layer;

// This harness edits only its own output copy of the settings file. No SQL is opened.
var path = Path.Combine(AppContext.BaseDirectory, "database-settings.json");
var originalFile = File.ReadAllBytes(path);
var originalEnvironment = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var originalDirectory = Environment.CurrentDirectory;
int checks = 0;
try
{
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, null);
    var originalConnection = DatabaseConnection.ConnectionString;
    if (originalConnection != @"Server=.\SQLEXPRESS;Database=UniversityTimetable;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;")
        throw new Exception("The shipped default changed.");
    checks++;

    Environment.CurrentDirectory = Path.GetTempPath();
    if (DatabaseConnection.ConnectionString != originalConnection)
        throw new Exception("Settings depend on the working directory.");
    checks++;

    foreach (var value in new[] { @"Server=INSTALLER\SQL;Database=Installed;Integrated Security=True;", "Server=SECOND;Database=Updated;Integrated Security=True;" })
    {
        File.WriteAllText(path, JsonSerializer.Serialize(new { ConnectionStrings = new { UniversityTimetable = value } }));
        if (DatabaseConnection.ConnectionString != value)
            throw new Exception("Edited JSON settings were not read.");
        checks++;
    }

    File.WriteAllText(path, "invalid json");
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, "Server=OVERRIDE;Database=Test;");
    if (DatabaseConnection.ConnectionString != "Server=OVERRIDE;Database=Test;")
        throw new Exception("Environment override no longer bypasses JSON.");
    checks++;
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, null);

    foreach (var test in new (string Json, Type Error)[]
    {
        ("invalid json", typeof(JsonException)),
        ("{}", typeof(KeyNotFoundException)),
        ("{\"ConnectionStrings\":{}}", typeof(KeyNotFoundException)),
        ("{\"ConnectionStrings\":{\"UniversityTimetable\":\" \"}}", typeof(InvalidOperationException)),
        ("{\"ConnectionStrings\":{\"UniversityTimetable\":null}}", typeof(InvalidOperationException))
    })
    {
        File.WriteAllText(path, test.Json);
        Exception? error = null;
        try { _ = DatabaseConnection.ConnectionString; }
        catch (Exception caught) { error = caught; }
        if (error is null || !test.Error.IsInstanceOfType(error))
            throw new Exception($"Expected {test.Error.Name} for invalid configuration.");
        checks++;
    }

    File.Delete(path);
    bool missingRejected = false;
    try { _ = DatabaseConnection.ConnectionString; }
    catch (FileNotFoundException) { missingRejected = true; }
    if (!missingRejected) throw new Exception("Missing settings should fail explicitly.");
    checks++;
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, "Server=OVERRIDE;Database=Test;");
    if (DatabaseConnection.ConnectionString != "Server=OVERRIDE;Database=Test;")
        throw new Exception("Environment override requires a settings file.");
    checks++;
    Console.WriteLine($"PASS {checks} database configuration checks; no database connection opened.");
}
finally
{
    File.WriteAllBytes(path, originalFile);
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, originalEnvironment);
    Environment.CurrentDirectory = originalDirectory;
}
