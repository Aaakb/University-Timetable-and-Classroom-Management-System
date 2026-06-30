namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleFacultySelector
    {
        public static IEnumerable<int> GetOrderedFacultyOptions(
            IReadOnlyList<int> facultyMemberIds,
            int semesterNumber,
            string dayOfWeek,
            IReadOnlyDictionary<(int FacultyMemberID, int SemesterNumber, string DayOfWeek), int> facultyDayLoads,
            IReadOnlyDictionary<int, int> facultyTotalLoads)
        {
            return facultyMemberIds
                .OrderBy(facultyMemberId => Count(facultyDayLoads, (facultyMemberId, semesterNumber, dayOfWeek)))
                .ThenBy(facultyMemberId => Count(facultyTotalLoads, facultyMemberId))
                .ThenBy(facultyMemberId => facultyMemberId);
        }

        private static int Count<TKey>(IReadOnlyDictionary<TKey, int> values, TKey key)
            where TKey : notnull
        {
            return values.TryGetValue(key, out int count) ? count : 0;
        }
    }
}
