using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class PracticalExchangeChecks
{
    private static void Ensure(bool condition, string message = "Exchange assertion failed")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Section Section(int id = 101, string name = "A", int branch = 201) =>
        new() { SectionID = id, SectionName = name, BranchID = branch, StudyYearID = 3, StudentCount = 30 };

    private static List<Schedule> SubjectGroups(Section section, int subjectId, int teacherId, int semester = 1) =>
        new[] { section.SectionName + "1", section.SectionName + "2" }.Select(group => new Schedule
        {
            Subject = new Subject { SubjectID = subjectId, SubjectName = "Practical " + subjectId, StudyYearID = 3,
                BranchID = section.BranchID, SemesterNumber = semester, PracticalHours = 1 },
            FacultyMemberID = teacherId, Section = section, LectureType = "Practical", GroupName = group
        }).ToList();

    private static List<Classroom> Rooms() => Enumerable.Range(1, 11).Select(id => new Classroom
    {
        ClassroomID = id, Capacity = 40, ClassroomNumber = id <= 4 ? "Lecture " + id : "Lab " + (id - 4),
        RoomType = id <= 4 ? "Lecture" : "Lab"
    }).ToList();

    private static List<TimeSlot> Periods() =>
    [
        new() { TimeSlotID = 10, StartTime = new(8, 30, 0), EndTime = new(9, 50, 0) },
        new() { TimeSlotID = 20, StartTime = new(10, 0, 0), EndTime = new(11, 20, 0) },
        new() { TimeSlotID = 30, StartTime = new(11, 30, 0), EndTime = new(12, 50, 0) }
    ];

    internal static void ValidateBlocks(List<Schedule> plan, List<TimeSlot> periods)
    {
        var ordered = periods.OrderBy(p => p.StartTime).ToList();
        foreach (var scope in plan.Where(s => s.LectureType == "Practical").GroupBy(s => new { s.SectionID, s.SemesterNumber }))
        {
            var subjects = scope.GroupBy(s => s.SubjectID).Select(g => g.OrderBy(s =>
                ordered.FindIndex(p => p.TimeSlotID == s.TimeSlotID)).ToList()).ToList();
            foreach (var subject in subjects)
                Ensure(subject.Count == 2 && subject.Select(s => s.GroupName).Distinct().Count() == 2);
            bool FixedPair(List<Schedule> subject) => subject.Select(s => s.DayOfWeek).Distinct().Count() == 1 &&
                subject.Select(s => s.ClassroomID).Distinct().Count() == 1 && subject.Select(s => s.FacultyMemberID).Distinct().Count() == 1 &&
                ordered.FindIndex(p => p.TimeSlotID == subject[1].TimeSlotID) == ordered.FindIndex(p => p.TimeSlotID == subject[0].TimeSlotID) + 1;
            int unmatched = subjects.Count(subject => !FixedPair(subject) || !subjects.Any(other =>
                other[0].SubjectID != subject[0].SubjectID && FixedPair(other) && other[0].DayOfWeek == subject[0].DayOfWeek &&
                other[0].TimeSlotID == subject[0].TimeSlotID && other[1].TimeSlotID == subject[1].TimeSlotID &&
                other[0].GroupName != subject[0].GroupName && other[1].GroupName != subject[1].GroupName &&
                other[0].ClassroomID != subject[0].ClassroomID && other[0].FacultyMemberID != subject[0].FacultyMemberID));
            Ensure(unmatched == subjects.Count % 2);
        }
        foreach (var period in plan.GroupBy(s => new { s.SemesterNumber, s.DayOfWeek, s.TimeSlotID }))
        {
            Ensure(period.Select(s => s.ClassroomID).Distinct().Count() == period.Count());
            Ensure(period.Select(s => s.FacultyMemberID).Distinct().Count() == period.Count());
            foreach (var section in period.GroupBy(s => s.SectionID))
                Ensure(section.Count() == 1 || section.All(s => !string.IsNullOrWhiteSpace(s.GroupName)) &&
                    section.Select(s => s.GroupName).Distinct().Count() == section.Count());
        }
    }

    public static async Task RunAsync(Func<string, Func<Task>, Task> check)
    {
        var rooms = Rooms(); var periods = Periods(); var a = Section();
        List<Schedule> Two(Section section) => [.. SubjectGroups(section, 1001, 501), .. SubjectGroups(section, 1002, 502)];
        async Task Test(string name, Action action) => await check(name, () => { action(); return Task.CompletedTask; });
        foreach (string name in new[] { "A", "B" })
            await Test(name + " groups exchange two subjects in one day and consecutive periods", () =>
            {
                var section = Section(name: name); var result = GeneratorProbe.Plan(Two(section), rooms, periods);
                Ensure(result.Schedules.Count == 4 && result.Issues.Count == 0);
                ValidateBlocks(result.Schedules, periods);
                Ensure(result.Schedules.Select(s => s.ClassroomID).Distinct().Count() == 2);
                Ensure(result.Schedules.Select(s => s.DayOfWeek).Distinct().Count() == 1);
                foreach (var slot in result.Schedules.GroupBy(s => s.TimeSlotID))
                    Ensure(slot.Count() == 2 && slot.Select(s => s.SubjectID).Distinct().Count() == 2 && slot.Select(s => s.GroupName).Distinct().Count() == 2);
            });
        await Test("A single odd subject teaches the two groups separately", () =>
        {
            var result = GeneratorProbe.Plan(SubjectGroups(a, 1001, 501), rooms, periods);
            Ensure(result.Schedules.Count == 2 && result.Issues.Count == 0); ValidateBlocks(result.Schedules, periods);
            var first = result.Schedules.OrderBy(s => periods.FindIndex(p => p.TimeSlotID == s.TimeSlotID)).First();
            Ensure(first.GroupName == "A1");
        });
        await Test("The odd-subject exception can use different days with only one daily period", () =>
        {
            var result = GeneratorProbe.Plan(SubjectGroups(a, 1001, 501), rooms, periods.Take(1).ToList());
            Ensure(result.Schedules.Count == 2 && result.Issues.Count == 0);
            Ensure(result.Schedules.Select(s => s.DayOfWeek).Distinct().Count() == 2);
            ValidateBlocks(result.Schedules, periods.Take(1).ToList());
        });
        await Test("The odd-subject exception can use different labs on different days", () =>
        {
            var twoLabs = rooms.Where(r => r.ClassroomID is 7 or 8).ToList();
            var occupied = new List<Schedule>(); int id = 900;
            foreach (string day in new[] { "Sunday", "Monday", "Wednesday", "Thursday" })
                foreach (var room in twoLabs)
                {
                    if (day == "Sunday" && room.ClassroomID == 7 || day == "Monday" && room.ClassroomID == 8) continue;
                    occupied.Add(new Schedule { SubjectID = id, SectionID = id, StudyYearID = 3, BranchID = id,
                        FacultyMemberID = id++, ClassroomID = room.ClassroomID, SemesterNumber = 1,
                        TimeSlotID = 10, DayOfWeek = day, LectureType = "Practical", GroupName = "A1" });
                }
            var plan = GeneratorProbe.PlaceOddWithOccupiedResources(SubjectGroups(a, 1001, 501), twoLabs, periods.Take(1).ToList(), occupied);
            Ensure(plan.Count == 2 && plan.Select(s => s.ClassroomID).Distinct().Count() == 2 &&
                plan.Select(s => s.DayOfWeek).Distinct().Count() == 2);
            ValidateBlocks(plan, periods.Take(1).ToList());
        });
        await Test("Three subjects form an exchange plus one separate odd-subject block", () =>
        {
            var result = GeneratorProbe.Plan([.. Two(a), .. SubjectGroups(a, 1003, 503)], rooms, periods);
            Ensure(result.Schedules.Count == 6 && result.Issues.Count == 0); ValidateBlocks(result.Schedules, periods);
        });
        await Test("Two paired subjects cannot fall back to single sessions with the same teacher", () =>
        {
            var result = GeneratorProbe.Plan([.. SubjectGroups(a, 1001, 501), .. SubjectGroups(a, 1002, 501)], rooms, periods);
            Ensure(result.Schedules.Count == 0 && result.Issues.Count > 0);
        });
        await Test("One lab cannot accommodate a two-subject exchange", () =>
        {
            var result = GeneratorProbe.Plan(Two(a), rooms.Where(r => r.ClassroomID == 7).ToList(), periods);
            Ensure(result.Schedules.Count == 0 && result.Issues.Count > 0);
        });
        await Test("One period cannot produce a partial exchange", () =>
        {
            var result = GeneratorProbe.Plan(Two(a), rooms, periods.Take(1).ToList());
            Ensure(result.Schedules.Count == 0 && result.Issues.Count > 0);
        });
        await Test("A missing counterpart group cannot produce partial bookings", () =>
        {
            var result = GeneratorProbe.Plan(Two(a).Take(3).ToList(), rooms, periods);
            Ensure(result.Schedules.Count == 0 && result.Issues.Count > 0);
        });
        await Test("A group belonging to another section is rejected", () =>
        {
            var requests = Two(a); requests[3].GroupName = "B2";
            var result = GeneratorProbe.Plan(requests, rooms, periods);
            Ensure(result.Schedules.Count == 0 && result.Issues.Count > 0);
        });
        await Test("Independent semesters never exchange subjects with each other", () =>
        {
            var result = GeneratorProbe.Plan([.. SubjectGroups(a, 1001, 501), .. SubjectGroups(a, 1002, 502, 2)], rooms, periods);
            Ensure(result.Schedules.Count == 4 && result.Issues.Count == 0); ValidateBlocks(result.Schedules, periods);
            Ensure(result.Schedules.GroupBy(s => new { s.SemesterNumber, s.DayOfWeek, s.TimeSlotID }).All(g => g.Count() == 1));
        });
        await Test("Independent branches retain their own group blocks", () =>
        {
            var result = GeneratorProbe.Plan([.. SubjectGroups(a, 1001, 501), .. SubjectGroups(Section(102, "A", 202), 1002, 502)], rooms, periods);
            Ensure(result.Schedules.Count == 4 && result.Issues.Count == 0); ValidateBlocks(result.Schedules, periods);
            Ensure(result.Schedules.GroupBy(s => new { s.SectionID, s.DayOfWeek, s.TimeSlotID }).All(g => g.Count() == 1));
        });
        await Test("The planner tries another partner when the first partner shares a teacher", () =>
        {
            var result = GeneratorProbe.Plan([.. SubjectGroups(a, 1001, 501), .. SubjectGroups(a, 1002, 501),
                .. SubjectGroups(a, 1003, 502), .. SubjectGroups(a, 1004, 502)], rooms, periods);
            Ensure(result.Schedules.Count == 8 && result.Issues.Count == 0); ValidateBlocks(result.Schedules, periods);
        });
        await Test("Busy early labs do not hide later available labs", () =>
        {
            var requests = new List<Schedule>();
            for (int i = 0; i < 3; i++)
                requests.AddRange([.. SubjectGroups(Section(101 + i, "A", 201 + i), 1001 + i * 2, 501 + i * 2),
                    .. SubjectGroups(Section(101 + i, "A", 201 + i), 1002 + i * 2, 502 + i * 2)]);
            var result = GeneratorProbe.Plan(requests, rooms, periods);
            Ensure(result.Schedules.Count == 12 && result.Issues.Count == 0); ValidateBlocks(result.Schedules, periods);
            Ensure(result.Schedules.Count(s => s.DayOfWeek == "Sunday" && s.TimeSlotID == 10) == 6);
        });
        await Test("Theory repair never splits an existing practical exchange", () =>
        {
            var requests = Two(a);
            for (int i = 0; i < 14; i++) requests.Add(new Schedule { Subject = new Subject { SubjectID = 2000 + i,
                SubjectName = "Theory " + i, StudyYearID = 3, BranchID = a.BranchID, SemesterNumber = 1, TheoreticalHours = 1 },
                FacultyMemberID = 600 + i, Section = a, LectureType = "Theory" });
            var result = GeneratorProbe.Plan(requests, rooms, periods);
            Ensure(result.Schedules.Count(s => s.LectureType == "Practical") == 4 && result.Issues.Count == 0);
            ValidateBlocks(result.Schedules, periods);
        });
        await Test("Missing labs report failure instead of distributing practical groups elsewhere", () =>
        {
            var result = GeneratorProbe.Plan(Two(a), rooms.Where(r => r.RoomType == "Lecture").ToList(), periods);
            Ensure(result.Schedules.Count == 0 && result.Issues.Count > 0);
        });
    }
}
