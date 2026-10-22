using System.Collections;
using System.Reflection;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class GeneratorProbe
{
    // Exercise the production planning algorithm without saving or replacing bookings.
    public static List<Schedule> Build(IReadOnlyList<Schedule> requests,
        IReadOnlyCollection<Classroom> rooms, IReadOnlyCollection<TimeSlot> periods) =>
        Plan(requests, rooms, periods).Schedules;

    public static ProbeResult Plan(IReadOnlyList<Schedule> requests,
        IReadOnlyCollection<Classroom> rooms, IReadOnlyCollection<TimeSlot> periods)
    {
        var assembly = typeof(ScheduleService).Assembly;
        const string prefix = "University_Timetable_and_Classroom_Management_System.BusinessLayer.";
        var requestType = assembly.GetType(prefix + "ScheduleGenerationRequest")!;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(requestType))!;
        foreach (var request in requests)
            list.Add(Activator.CreateInstance(requestType,
                [request.Subject, new List<int> { request.FacultyMemberID }, request.Section!,
                    1, 1, request.LectureType, request.GroupName])!);
        var context = Activator.CreateInstance(assembly.GetType(prefix + "ScheduleBuildContext")!, [rooms, periods]);
        var result = assembly.GetType(prefix + "ScheduleGenerator")!.GetMethod("BuildPlan")!.Invoke(null, [list, context])!;
        return new ProbeResult((List<Schedule>)result.GetType().GetProperty("Schedules")!.GetValue(result)!,
            ((IReadOnlyList<string>)result.GetType().GetProperty("PracticalExchangeIssues")!.GetValue(result)!).ToList(),
            (int)result.GetType().GetProperty("ConflictCount")!.GetValue(result)!);
    }

    public static List<Schedule> PlaceOddWithOccupiedResources(List<Schedule> requests,
        List<Classroom> rooms, List<TimeSlot> periods, List<Schedule> occupied)
    {
        var assembly = typeof(ScheduleService).Assembly;
        const string prefix = "University_Timetable_and_Classroom_Management_System.BusinessLayer.";
        var dictionary = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(
            assembly.GetType(prefix + "ScheduleDayPlanKey")!, assembly.GetType(prefix + "ScheduleDayPlan")!))!;
        var stateType = assembly.GetType(prefix + "ScheduleGenerationState")!;
        var state = Activator.CreateInstance(stateType, [dictionary])!;
        foreach (var booking in occupied) stateType.GetMethod("Add")!.Invoke(state, [booking]);
        var requestType = assembly.GetType(prefix + "ScheduleGenerationRequest")!;
        var candidateType = assembly.GetType(prefix + "SchedulePlacementCandidate")!;
        var generator = assembly.GetType(prefix + "ScheduleGenerator")!;
        object RequestPlan(Schedule prototype)
        {
            var request = Activator.CreateInstance(requestType, [prototype.Subject,
                new List<int> { prototype.FacultyMemberID }, prototype.Section!, 1, 1, "Practical", prototype.GroupName])!;
            var candidates = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(candidateType))!;
            var enumerable = (IEnumerable)generator.GetMethod("CreatePlacementCandidates", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [request, rooms, periods, dictionary])!;
            foreach (var candidate in enumerable) candidates.Add(candidate);
            return Activator.CreateInstance(assembly.GetType(prefix + "ScheduleRequestPlan")!, [request, candidates, 0])!;
        }
        generator.GetMethod("TryPlaceSinglePracticalSubject", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [RequestPlan(requests[0]), RequestPlan(requests[1]), state]);
        return ((List<Schedule>)stateType.GetProperty("Schedules")!.GetValue(state)!)
            .Where(s => s.SubjectID == requests[0].Subject.SubjectID && s.SectionID == requests[0].Section!.SectionID).ToList();
    }
}

internal sealed record ProbeResult(List<Schedule> Schedules, List<string> Issues, int Conflicts);
