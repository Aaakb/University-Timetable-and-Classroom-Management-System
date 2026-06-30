namespace University_Timetable_and_Classroom_Management_System.Models
{
    public class TimeSlot
    {
        public int TimeSlotID { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsBreak { get; set; }

        public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
