using System.Text.Json;

namespace Data_Access_Layer
{
    public static class DatabaseConnection
    {
        public const string EnvironmentVariableName = "UNIVERSITY_TIMETABLE_CONNECTION_STRING";

        public static string ConnectionString
        {
            get
            {
                var environmentConnection = Environment.GetEnvironmentVariable(EnvironmentVariableName);
                if (environmentConnection is not null)
                    return environmentConnection;

                var path = Path.Combine(AppContext.BaseDirectory, "database-settings.json");
                using var settings = JsonDocument.Parse(File.ReadAllText(path));
                var connection = settings.RootElement
                    .GetProperty("ConnectionStrings")
                    .GetProperty("UniversityTimetable")
                    .GetString();

                if (string.IsNullOrWhiteSpace(connection))
                    throw new InvalidOperationException(
                        $"ConnectionStrings:UniversityTimetable is empty in '{path}'.");

                return connection;
            }
        }
    }
}
