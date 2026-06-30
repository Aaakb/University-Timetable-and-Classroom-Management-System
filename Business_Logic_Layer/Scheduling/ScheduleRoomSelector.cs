using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class ScheduleRoomSelector
    {
        public static bool HasCapacity(Classroom classroom, Section section)
        {
            return HasCapacity(classroom, section, null);
        }

        public static bool HasCapacity(Classroom classroom, Section section, string? groupName)
        {
            return classroom.Capacity >= GetRequiredCapacity(section, groupName);
        }

        public static int GetRequiredCapacity(Section section, string? groupName)
        {
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                return Math.Max(1, (int)Math.Ceiling(section.StudentCount / 2.0));
            }

            return section.StudentCount;
        }

        public static void EnsureMatchesLectureType(Classroom classroom, string lectureType)
        {
            if (MatchesLectureType(classroom, lectureType))
            {
                return;
            }

            if (string.Equals(lectureType, "Practical", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Practical sessions must be assigned to a lab.");
            }

            throw new ArgumentException("Theory sessions must be assigned to a lecture room.");
        }

        public static List<Classroom> GetTargetClassrooms(
            IReadOnlyCollection<Classroom> classrooms,
            int requiredCapacity,
            string lectureType)
        {
            return classrooms
                .Where(classroom => classroom.Capacity >= requiredCapacity)
                .OrderBy(classroom => classroom.Capacity)
                .ThenBy(classroom => classroom.ClassroomNumber)
                .Where(classroom => MatchesLectureType(classroom, lectureType))
                .ToList();
        }

        public static bool MatchesLectureType(Classroom classroom, string lectureType)
        {
            bool isLab = string.Equals(classroom.RoomType, "Lab", StringComparison.OrdinalIgnoreCase) ||
                classroom.ClassroomNumber.Contains("Lab", StringComparison.OrdinalIgnoreCase);

            return string.Equals(lectureType, "Practical", StringComparison.OrdinalIgnoreCase)
                ? isLab
                : !isLab;
        }
    }
}
