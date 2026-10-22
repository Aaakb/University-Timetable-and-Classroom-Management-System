using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class CurrentDataPlanner
{
    // Deliberately calls only the pure planner, never ScheduleService.GenerateAsync.
    public static async Task Run()
    {
        var subjects = await new SubjectService().GetAllAsync();
        var assignments = await new FacultyMemberSubjectService().GetAllAsync();
        var sections = (await new SectionService().GetAllAsync()).Where(s =>
            !AcademicStructureRules.UsesGeneralSections(s.StudyYearID) || (!s.BranchID.HasValue &&
            AcademicStructureRules.GetAllowedSectionNames(s.StudyYearID).Contains(s.SectionName.Trim(), StringComparer.OrdinalIgnoreCase))).ToList();
        var rooms = (await new ClassroomService().GetAllAsync()).OrderBy(x => x.Capacity).ThenBy(x => x.ClassroomNumber).ToList();
        var periods = await new TimeSlotService().GetAllAsync();
        var assembly = typeof(ScheduleService).Assembly;
        const string prefix = "University_Timetable_and_Classroom_Management_System.BusinessLayer.";
        var generator = assembly.GetType(prefix + "ScheduleGenerator")!;
        var requestType = assembly.GetType(prefix + "ScheduleGenerationRequest")!;
        var requests = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(requestType))!;
        foreach (var subject in subjects)
        {
            var faculty = assignments.Where(x => x.SubjectID == subject.SubjectID).Select(x => x.FacultyMemberID).Distinct().Order().ToList();
            if (faculty.Count == 0) throw new Exception("Current data has unassigned subjects.");
            var teaching = Activator.CreateInstance(assembly.GetType(prefix + "SubjectTeachingAssignment")!, subject, faculty)!;
            foreach (var section in sections.Where(s => s.StudyYearID == subject.StudyYearID &&
                AcademicStructureRules.SectionMatchesSubject(subject.StudyYearID, subject.BranchID, s.BranchID, s.SectionName)))
                foreach (var request in (IEnumerable)generator.GetMethod("CreateScheduleRequests")!.Invoke(null, [teaching, section])!) requests.Add(request);
        }
        Subject SubjectOf(object r) => (Subject)requestType.GetProperty("Subject")!.GetValue(r)!;
        Section SectionOf(object r) => (Section)requestType.GetProperty("Section")!.GetValue(r)!;
        var sorted = requests.Cast<object>().OrderByDescending(r => SubjectOf(r).StudyYearID)
            .ThenBy(r => SubjectOf(r).BranchID ?? SectionOf(r).BranchID ?? 0).ThenBy(r => SubjectOf(r).SemesterNumber)
            .ThenBy(r => SubjectOf(r).SubjectName).ThenBy(r => (int)requestType.GetProperty("LessonNumber")!.GetValue(r)!)
            .ThenBy(r => SectionOf(r).SectionName).ToList();
        requests.Clear(); foreach (var request in sorted) requests.Add(request);
        var context = Activator.CreateInstance(assembly.GetType(prefix + "ScheduleBuildContext")!, rooms, periods)!;
        context.GetType().GetProperty("RequirePracticalExchange")!.SetValue(context, false);
        var watch = Stopwatch.StartNew();
        var result = generator.GetMethod("BuildPlan")!.Invoke(null, [requests, context])!;
        watch.Stop();
        var schedules = (List<Schedule>)result.GetType().GetProperty("Schedules")!.GetValue(result)!;
        var conflicts = (bool)assembly.GetType(prefix + "ScheduleConflictChecker")!.GetMethod("HasAnyPairConflict")!.Invoke(null, [schedules])!;
        var issues = (IReadOnlyList<string>)result.GetType().GetProperty("PracticalExchangeIssues")!.GetValue(result)!;
        var report = new { required = requests.Count, planned = schedules.Count, conflicts, practicalIssues = issues.Count,
            elapsedMilliseconds = watch.ElapsedMilliseconds, persisted = false };
        Directory.CreateDirectory(".tmp/WebIntegration");
        await File.WriteAllTextAsync(".tmp/WebIntegration/current-plan.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        if (requests.Count != schedules.Count || conflicts || issues.Count > 0) throw new Exception("Read-only planning did not produce a complete valid result.");
    }
}
