using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static partial class Program
{
    static async Task AlternativesChecksAsync()
    {
        var service = new TimetableAlternativesService(factory);
        string Signature(TimetableAlternative option) => string.Join(";", option.Lectures.OrderBy(l => l.Id).Select(l => $"{l.Id}/{l.Day}/{l.Period}"));
        await Check("Whole timetable alternatives are temporally distinct and previews never write", async () =>
        {
            string before = Json(await Rows()); var result = await service.GenerateAsync(new(1, 3, 10));
            Assert(result.Alternatives.Count == 3, result.Message);
            Assert(result.Alternatives.Select(Signature).Append(Signature(result.Current)).Distinct().Count() == 4);
            Assert(result.Alternatives.All(a => a.Metrics.ChangedLectures > 0 && a.Lectures.Count == initial.Count));
            Assert(before == Json(await Rows()));
            File.WriteAllText(Path.Combine(output, "timetable-alternatives.json"), Json(result));
        });
        await Check("Whole timetable alternatives honor two-day preference matrices without worsening baseline preference penalty", async () =>
        {
            await preferences.SaveAsync(Profile(), 0); await preferences.SaveAsync(Profile(2, 2), 0);
            var result = await service.GenerateAsync(new(1, 3, 10)); Assert(result.Alternatives.Count > 0, result.Message);
            foreach (var alternative in result.Alternatives)
            {
                Assert(alternative.Metrics.PreferencePenalty <= result.Current.Metrics.PreferencePenalty);
                foreach (string faculty in new[] { "د. أحمد", "د. سارة" }) Assert(alternative.Lectures.Where(l => l.Faculty == faculty).Select(l => l.Day).ToHashSet().SetEquals(new[] { "Sunday", "Wednesday" }));
            }
        });
        await Check("Choosing a whole timetable alternative requires confirmation and keeps lecture identities", async () =>
        {
            var result = await service.GenerateAsync(new(1, 2, 8)); var chosen = result.Alternatives.First(); string before = Json(await Rows());
            await Reject(() => service.SaveAsync(chosen, false)); Assert(before == Json(await Rows()));
            int count = await service.SaveAsync(chosen, true); Assert(count == chosen.Metrics.ChangedLectures && count > 0);
            var rows = await Rows(); Assert(rows.Select(s => s.ScheduleID).SequenceEqual(initial.Select(s => s.ScheduleID)));
            foreach (var old in initial) { var saved = rows.Single(s => s.ScheduleID == old.ScheduleID); var expected = Copy(old); expected.DayOfWeek = saved.DayOfWeek; expected.TimeSlotID = saved.TimeSlotID; expected.ClassroomID = saved.ClassroomID; Assert(Json(expected) == Json(saved)); }
        });
        await Check("Imported locked timetables explain why no alternatives exist and never silently unlock", async () =>
        {
            await using (var db = factory()) await db.Schedules.ExecuteUpdateAsync(u => u.SetProperty(s => s.IsLocked, true));
            string before = Json(await Rows()); var result = await service.GenerateAsync(new(1, 3, 5));
            Assert(result.Alternatives.Count == 0 && result.Message.Contains("مثبتة")); Assert(before == Json(await Rows()));
            var ids = initial.Where(s => s.FacultyMemberID == 1).Select(s => s.ScheduleID).ToArray();
            Assert(await service.SetLocksAsync(1, ids, false, result.Version) == 2);
            var after = await Rows(); Assert(after.Where(s => s.FacultyMemberID == 2).All(s => s.IsLocked));
            var unlocked = await service.GenerateAsync(new(1, 2, 8)); Assert(unlocked.Alternatives.Count > 0, unlocked.Message);
            Assert(unlocked.Alternatives.All(a => a.Lectures.Where(l => l.Locked).All(l => !l.Changed)));
        });
        await Check("Single feasible timetable does not return duplicates or cosmetic room-only alternatives", async () =>
        {
            foreach (int faculty in new[] { 1, 2 })
            {
                int period = faculty == 1 ? 1 : 2; var p = Profile(faculty);
                p = p with { PeriodPreferences = p.PeriodPreferences.Select(c => c with { Level = p.SelectedDays.Contains(c.Day) && c.TimeSlotId == period ? FacultyPreferenceLevel.StronglyPreferred : FacultyPreferenceLevel.Prohibited }).ToArray() };
                await preferences.SaveAsync(p, 0);
            }
            await using (var db = factory()) await db.Schedules.Where(s => s.SubjectID == 1 || s.SubjectID == 3).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsLocked, true));
            var result = await service.GenerateAsync(new(1, 3, 8)); Assert(result.Alternatives.Count == 0 && result.Message.Contains("لا يوجد بديل"), result.Message);
        });
        await Check("Changing preferences or locks invalidates whole timetable approvals", async () =>
        {
            var result = await service.GenerateAsync(new(1, 1, 5));
            await preferences.SaveAsync(Profile(), 0); string before = Json(await Rows());
            await Reject(() => service.SaveAsync(result.Alternatives.First(), true));
            await Reject(() => service.SetLocksAsync(1, [initial[0].ScheduleID], true, result.Version));
            Assert(before == Json(await Rows()));
        });
        await Check("Canceled whole timetable alternative search preserves the database", async () =>
        {
            string before = Json(await Rows()); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Reject(() => service.GenerateAsync(new(1), cancellation.Token)); Assert(before == Json(await Rows()));
        });
    }
}
