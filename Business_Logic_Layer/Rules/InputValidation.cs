namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class InputValidation
    {
        public static async Task RecordIdAsync(int id, bool isUpdate, Func<Task<bool>> exists, string recordName)
        {
            if (id <= 0)
                throw new ArgumentException($"{recordName} ID is required.");

            // Keep the existence query after the required-ID check and before
            // the remaining field validation in each caller.
            bool idExists = await exists();
            if (!isUpdate && idExists)
                throw new ArgumentException($"{recordName} ID already exists.");
            if (isUpdate && !idExists)
                throw new KeyNotFoundException($"{recordName} not found.");
        }

        public static void TextLength(string? value, int maximum, string field)
        {
            if (value?.Trim().Length > maximum)
                throw new ArgumentException($"{field} must be {maximum} characters or fewer.");
        }
    }
}
