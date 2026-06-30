namespace Data_Access_Layer
{
    public static class DatabaseConnection
    {
        public const string EnvironmentVariableName = "UNIVERSITY_TIMETABLE_CONNECTION_STRING";

        public static string ConnectionString =>
            Environment.GetEnvironmentVariable(EnvironmentVariableName)
            ?? @"Server=.\SQLEXPRESS;Database=UniversityTimetable;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";
    }
}
