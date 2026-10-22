namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class InputValidation
    {
        public static void TextLength(string? value, int maximum, string field)
        {
            if (value?.Trim().Length > maximum)
                throw new ArgumentException($"{field} must be {maximum} characters or fewer.");
        }
    }
}
