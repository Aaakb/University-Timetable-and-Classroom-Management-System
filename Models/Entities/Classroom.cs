namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class Classroom
    {
        public int ClassroomID { get; set; }

        public string ClassroomNumber { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public string? RoomType { get; set; }

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
