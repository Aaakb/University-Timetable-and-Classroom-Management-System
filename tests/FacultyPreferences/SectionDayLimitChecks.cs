using System.Reflection;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static partial class Program
{
    static async Task SeedFourFixedDaysAsync(bool otherSection = false)
    {
        await using var db = factory(); await db.Schedules.ExecuteDeleteAsync();
        foreach (string day in days.Take(4))
        {
            var row = Copy(initial.First(s => s.FacultyMemberID == 2));
            row.ScheduleID = 0; row.SectionID = 1; row.DayOfWeek = day; row.IsLocked = true; row.TimeSlotID = 2;
            db.Schedules.Add(row);
        }
        var movable = Copy(initial.First(s => s.FacultyMemberID == 1));
        movable.ScheduleID = 0; movable.SectionID = otherSection ? 2 : 1; movable.DayOfWeek = "Thursday";
        db.Schedules.Add(movable); await db.SaveChangesAsync();
    }
    static async Task SectionDayLimitChecksAsync()
    {
        await Check("Soft faculty preferences cannot add a fifth day to a section with four locked teaching days", async () =>
        {
            await SeedFourFixedDaysAsync(); var before = await Rows();
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(preferred: "Thursday"), 0);
            var after = await Rows();
            Assert(result.ChangedLectures == 1 && after.Where(s => s.SectionID == 1).Select(s => s.DayOfWeek).Distinct().Count() == 4);
            Assert(after.Single(s => s.FacultyMemberID == 1).DayOfWeek != "Thursday");
            foreach (var old in before.Where(s => s.IsLocked)) Assert(Json(after.Single(s => s.ScheduleID == old.ScheduleID)) == Json(old));
        });
        await Check("A prohibited-day preference that requires a fifth section day rolls back preferences and timetable", async () =>
        {
            await SeedFourFixedDaysAsync(); string before = Json(await Rows());
            await Reject(() => editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(preferred: "Thursday", onlyPreferredDay: true), 0));
            Assert(before == Json(await Rows()) && await preferences.GetAsync(1, 1) is null);
        });
        await Check("Different sections may use different four-day sets and a year may span all five days", async () =>
        {
            await SeedFourFixedDaysAsync(otherSection: true);
            var result = await editor.SaveDayPreferencesAndOptimizeAsync(DayProfile(preferred: "Thursday", onlyPreferredDay: true), 0);
            var after = await Rows();
            Assert(result.ChangedLectures == 0 && after.Select(s => s.DayOfWeek).Distinct().Count() == 5);
            Assert(after.GroupBy(s => (s.SemesterNumber, s.SectionID)).All(g => g.Select(s => s.DayOfWeek).Distinct().Count() <= 4));
        });
        await Check("Individual faculty optimization includes fixed lectures of other faculty in the four-day limit", async () =>
        {
            await SeedFourFixedDaysAsync(); var p = DayProfile(preferred: "Thursday");
            await preferences.SaveAsync(p, 0);
            var preview = await editor.PreviewAsync(p);
            Assert(preview.CanSave && preview.Changes.Single().NewDay != "Thursday", preview.Message);
            await editor.SaveAsync(preview, true);
            Assert((await Rows()).Select(s => s.DayOfWeek).Distinct().Count() == 4);
        });
        await Check("Final validation rejects a modified preview that reintroduces a fifth section day", async () =>
        {
            await SeedFourFixedDaysAsync(); var p = DayProfile(preferred: "Thursday");
            var preview = await editor.PreviewAsync(p); Assert(preview.CanSave);
            var plan = (IReadOnlyList<Schedule>)typeof(FacultySchedulePreview).GetProperty("Plan", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(preview)!;
            plan.Single().DayOfWeek = "Thursday";
            string before = Json(await Rows()); bool blocked = false;
            try { await editor.SaveAsync(preview, true); }
            catch (ArgumentException ex) { blocked = ex.Message.Contains("الحد الأقصى: 4") && ex.Message.Contains("عدد أيام التدريس: 5"); }
            Assert(blocked && Json(await Rows()) == before, "Final validation did not enforce the section-day constraint");
        });
    }
}
