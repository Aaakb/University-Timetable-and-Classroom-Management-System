using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal sealed record ScheduleBuildContext(
        IReadOnlyCollection<Classroom> Classrooms,
        IReadOnlyCollection<TimeSlot> TimeSlots);
}
