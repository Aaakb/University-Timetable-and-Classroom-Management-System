using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    internal static class SchedulingResourceMaintenance
    {
        private const int MinimumLectureRoomCount = 5;
        private const int MinimumLabCount = 6;
        private const int DefaultCapacity = 40;

        public static async Task<SchedulingResourceMaintenanceResult> EnsureOfficialResourcesAsync(AppDbContext context)
        {
            int addedTimeSlots = await EnsureOfficialTimeSlotsAsync(context);
            await NormalizeClassroomsAsync(context);
            int addedClassrooms = await EnsureMinimumClassroomsAsync(context);

            return new SchedulingResourceMaintenanceResult(
                addedTimeSlots,
                addedClassrooms,
                0,
                0);
        }

        private static async Task<int> EnsureOfficialTimeSlotsAsync(AppDbContext context)
        {
            var timeSlots = await context.TimeSlots.ToListAsync();
            var lectureSlots = timeSlots
                .Where(slot => !slot.IsBreak)
                .OrderBy(slot => slot.StartTime)
                .ThenBy(slot => slot.TimeSlotID)
                .ToList();
            var usedIds = timeSlots.Select(slot => slot.TimeSlotID).ToHashSet();
            int addedCount = 0;
            int updatedCount = 0;

            foreach (var officialSlot in ScheduleTimingRules.OfficialLectureSlots)
            {
                bool exists = timeSlots.Any(slot =>
                    !slot.IsBreak &&
                    slot.StartTime == officialSlot.Start &&
                    slot.EndTime == officialSlot.End);

                if (exists)
                {
                    continue;
                }

                var reusableSlot = lectureSlots.FirstOrDefault(slot =>
                    !ScheduleTimingRules.IsOfficialLectureSlot(slot));

                if (reusableSlot is not null)
                {
                    reusableSlot.StartTime = officialSlot.Start;
                    reusableSlot.EndTime = officialSlot.End;
                    reusableSlot.IsBreak = false;
                    updatedCount++;
                    continue;
                }

                var timeSlot = new TimeSlot
                {
                    TimeSlotID = NextAvailableId(usedIds),
                    StartTime = officialSlot.Start,
                    EndTime = officialSlot.End,
                    IsBreak = false
                };

                await context.TimeSlots.AddAsync(timeSlot);
                timeSlots.Add(timeSlot);
                lectureSlots.Add(timeSlot);
                addedCount++;
            }

            if (addedCount > 0)
            {
                await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[TimeSlots]");
            }
            else if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
            }

            return addedCount + updatedCount;
        }

        private static async Task<int> EnsureMinimumClassroomsAsync(AppDbContext context)
        {
            var classrooms = await context.Classrooms.ToListAsync();
            var usedIds = classrooms.Select(classroom => classroom.ClassroomID).ToHashSet();
            var usedNames = classrooms
                .Select(classroom => classroom.ClassroomNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int capacity = Math.Max(
                DefaultCapacity,
                await context.Sections.MaxAsync(section => (int?)section.StudentCount) ?? DefaultCapacity);

            int lectureRoomCount = classrooms.Count(IsLectureRoom);
            int labCount = classrooms.Count(IsLab);
            int addedCount = 0;

            while (lectureRoomCount < MinimumLectureRoomCount)
            {
                var classroom = CreateClassroom(
                    usedIds,
                    usedNames,
                    "Lecture Hall",
                    "Lecture",
                    capacity);

                await context.Classrooms.AddAsync(classroom);
                classrooms.Add(classroom);
                lectureRoomCount++;
                addedCount++;
            }

            while (labCount < MinimumLabCount)
            {
                var classroom = CreateClassroom(
                    usedIds,
                    usedNames,
                    "Lab",
                    "Lab",
                    capacity);

                await context.Classrooms.AddAsync(classroom);
                classrooms.Add(classroom);
                labCount++;
                addedCount++;
            }

            if (addedCount > 0)
            {
                await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[Classrooms]");
            }

            return addedCount;
        }

        private static async Task NormalizeClassroomsAsync(AppDbContext context)
        {
            var classrooms = await context.Classrooms
                .OrderBy(classroom => classroom.ClassroomID)
                .ToListAsync();

            if (classrooms.Count == 0)
            {
                return;
            }

            var lectureRooms = classrooms
                .Where(classroom => !IsLab(classroom))
                .OrderBy(classroom => classroom.ClassroomID)
                .ToList();

            var labs = classrooms
                .Where(IsLab)
                .OrderBy(classroom => classroom.ClassroomID)
                .ToList();

            bool needsRename = NeedsClassroomNormalization(lectureRooms, "Lecture Hall", "Lecture") ||
                NeedsClassroomNormalization(labs, "Lab", "Lab");

            if (!needsRename)
            {
                return;
            }

            foreach (var classroom in classrooms)
            {
                classroom.ClassroomNumber = $"__RoomTemp_{classroom.ClassroomID}";
            }

            await context.SaveChangesAsync();

            RenameClassroomGroup(lectureRooms, "Lecture Hall", "Lecture");
            RenameClassroomGroup(labs, "Lab", "Lab");

            await context.SaveChangesAsync();
        }

        private static bool NeedsClassroomNormalization(
            IReadOnlyList<Classroom> classrooms,
            string namePrefix,
            string roomType)
        {
            for (int index = 0; index < classrooms.Count; index++)
            {
                string expectedName = $"{namePrefix} {index + 1:00}";

                if (!string.Equals(classrooms[index].ClassroomNumber, expectedName, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(classrooms[index].RoomType, roomType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RenameClassroomGroup(
            IReadOnlyList<Classroom> classrooms,
            string namePrefix,
            string roomType)
        {
            for (int index = 0; index < classrooms.Count; index++)
            {
                classrooms[index].ClassroomNumber = $"{namePrefix} {index + 1:00}";
                classrooms[index].RoomType = roomType;
            }
        }

        private static Classroom CreateClassroom(
            HashSet<int> usedIds,
            HashSet<string> usedNames,
            string namePrefix,
            string roomType,
            int capacity)
        {
            return new Classroom
            {
                ClassroomID = NextAvailableId(usedIds),
                ClassroomNumber = NextClassroomName(usedNames, namePrefix),
                Capacity = capacity,
                RoomType = roomType
            };
        }

        private static int NextAvailableId(HashSet<int> usedIds)
        {
            int nextId = 1;

            while (usedIds.Contains(nextId))
            {
                nextId++;
            }

            usedIds.Add(nextId);
            return nextId;
        }

        private static string NextClassroomName(HashSet<string> usedNames, string prefix)
        {
            int number = 1;
            string roomName;

            do
            {
                roomName = $"{prefix} {number:00}";
                number++;
            }
            while (usedNames.Contains(roomName));

            usedNames.Add(roomName);
            return roomName;
        }

        private static bool IsLab(Classroom classroom)
        {
            return string.Equals(classroom.RoomType, "Lab", StringComparison.OrdinalIgnoreCase) ||
                classroom.ClassroomNumber.Contains("Lab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLectureRoom(Classroom classroom)
        {
            return !IsLab(classroom);
        }
    }

    internal sealed record SchedulingResourceMaintenanceResult(
        int AddedTimeSlots,
        int AddedClassrooms,
        int AddedFacultyMembers,
        int AddedFacultySubjectAssignments);

}
