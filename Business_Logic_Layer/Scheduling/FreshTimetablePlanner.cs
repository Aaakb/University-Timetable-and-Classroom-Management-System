using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

// The fast placer can leave a few requests pending even when a complete plan
// exists. Fresh generation must give the constraint solver every movable request,
// including the unplaced ones, before concluding that the timetable cannot fit.
internal static class FreshTimetablePlanner
{
    internal sealed record Result(List<Schedule>? Schedules, string Status, IReadOnlyList<string> Diagnostics);

    internal static async Task<Result> CompleteAsync(
        IReadOnlyList<ScheduleGenerationRequest> requests,
        IReadOnlyList<Schedule> fixedRows,
        IReadOnlyList<Schedule> partial,
        ImportCatalog catalog)
    {
        var rooms = catalog.Rows<Classroom>();
        var periods = catalog.Rows<TimeSlot>().Where(p => !p.IsBreak).OrderBy(p => p.StartTime).ToList();
        var candidates = partial.Except(fixedRows).ToList();
        var complete = new List<Schedule>(fixedRows);
        foreach (var semester in requests.GroupBy(r => r.Subject.SemesterNumber))
        {
            System.Diagnostics.Trace.WriteLine($"Fresh constraint fallback: semester={semester.Key}, requests={semester.Count()}, fixed={fixedRows.Count}");
            var rows = new List<TimetableImportRow>();
            foreach (var request in semester)
            {
                var placed = candidates.FirstOrDefault(s => s.SubjectID == request.Subject.SubjectID &&
                    s.SectionID == request.Section.SectionID && s.SemesterNumber == request.Subject.SemesterNumber &&
                    s.LectureType == request.LectureType && string.Equals(s.GroupName, request.GroupName, StringComparison.OrdinalIgnoreCase) &&
                    request.FacultyMemberIds.Contains(s.FacultyMemberID));
                Schedule seed;
                if (placed is not null)
                {
                    candidates.Remove(placed);
                    seed = FacultyCpSatPlanner.Copy(placed);
                }
                else
                {
                    var room = ScheduleRoomSelector.GetTargetClassrooms(rooms,
                        ScheduleRoomSelector.GetRequiredCapacity(request.Section, request.GroupName), request.LectureType).FirstOrDefault();
                    if (room is null)
                        return new(null, "Infeasible", [$"لا توجد قاعة مناسبة بسعة كافية لمادة «{request.Subject.SubjectName}» للشعبة {request.Section.SectionName} ({request.LectureType})."]);
                    seed = new Schedule
                    {
                        SubjectID = request.Subject.SubjectID, FacultyMemberID = request.FacultyMemberIds[0],
                        SemesterNumber = request.Subject.SemesterNumber, StudyYearID = request.Subject.StudyYearID,
                        BranchID = request.Subject.BranchID ?? request.Section.BranchID, SectionID = request.Section.SectionID,
                        LectureType = request.LectureType, GroupName = request.GroupName,
                        DayOfWeek = ScheduleDayRules.AllDays[0], TimeSlotID = periods[0].TimeSlotID, ClassroomID = room.ClassroomID
                    };
                }
                seed.ScheduleID = 0;
                rows.Add(new("Fresh generation", rows.Count + 1, seed, false));
            }
            // Each semester has independent room/teacher/student bookings. Never
            // couple their resource constraints by submitting both in one model.
            var result = await Task.Run(() => ImportedTimetableRepairPlanner.Solve(catalog, rows,
                fixedRows, 55, default, optimizePreferences: true,
                allowFacultyReassignment: true, requirePracticalExchange: false));
            System.Diagnostics.Trace.WriteLine($"Fresh constraint fallback result: semester={semester.Key}, status={result.Status}, placed={result.Plan?.Count ?? 0}");
            if (result.Plan is null) return new(null, result.Status, result.Diagnostics);
            complete.AddRange(result.Plan);
        }
        return new(complete, "Feasible", []);
    }
}
