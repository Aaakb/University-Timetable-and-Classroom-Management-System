using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public static class ClassroomRules
    {
        // Initial setup defaults only, not counts or limits for the live database.
        public const int LectureRoomCount = 4;
        public const int LabCount = 7;
        public const int SharedLabCount = 2;
        public const string SharedLabType = "Shared Lab";

        public static bool IsSharedLab(Classroom classroom) =>
            string.Equals(classroom.RoomType?.Trim(), SharedLabType, StringComparison.OrdinalIgnoreCase);

        public static bool IsLab(Classroom classroom) =>
            NormalizeRoomType(classroom.RoomType, classroom.ClassroomNumber) != "Lecture";

        internal static string NormalizeRoomType(string? roomType, string classroomNumber)
        {
            string value = roomType?.Trim() ?? string.Empty;
            if (string.Equals(value, SharedLabType, StringComparison.OrdinalIgnoreCase))
                return SharedLabType;
            if (string.Equals(value, "Lecture", StringComparison.OrdinalIgnoreCase))
                return "Lecture";
            if (string.Equals(value, "Lab", StringComparison.OrdinalIgnoreCase))
                return "Lab";

            // Preserve compatibility with legacy records without a recognized type.
            // An explicit selection always takes precedence over a room's name.
            return value.Contains("Lab", StringComparison.OrdinalIgnoreCase) ||
                classroomNumber.Contains("Lab", StringComparison.OrdinalIgnoreCase)
                ? "Lab" : "Lecture";
        }
    }
}
