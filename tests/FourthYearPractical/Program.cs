using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.API.Controllers;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

string database = "UniversityTimetable_FourthYear_" + Guid.NewGuid().ToString("N")[..8];
var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = database };
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
Console.WriteLine("ISOLATED DATABASE " + database);
int checks = 0;
void Check(bool passed, string message) { if (!passed) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
async Task<List<Schedule>> Rows() { await using var db = new AppDbContext(); return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); }
string Fingerprint(IEnumerable<Schedule> rows) => JsonSerializer.Serialize(rows);
string Backup(string name) => Path.Combine(Path.GetTempPath(), "FourthYearPractical", database, name + ".uttbackup.json");
await using (var db = new AppDbContext())
{
    await db.Database.EnsureCreatedAsync();
    db.StudyYears.AddRange(new StudyYear { StudyYearID = 1, YearName = "First Year" }, new StudyYear { StudyYearID = 3, YearName = "Third Year" }, new StudyYear { StudyYearID = 4, YearName = "Fourth Year" });
    for (int id = 1; id <= 5; id++)
    {
        db.FacultyMembers.Add(new() { FacultyMemberID = id, FullName = "Teacher " + id });
        db.Classrooms.Add(new() { ClassroomID = id, ClassroomNumber = "Lab " + id, RoomType = "Lab", Capacity = 30 });
        db.Classrooms.Add(new() { ClassroomID = id + 10, ClassroomNumber = "Lecture " + id, RoomType = "Lecture", Capacity = 40 });
        if (id <= 3) db.Branches.Add(new() { BranchID = id, BranchName = "Branch " + id });
        db.Sections.Add(new() { SectionID = id, SectionName = "A", StudentCount = 30, StudyYearID = id <= 3 ? 4 : id == 4 ? 3 : 1, BranchID = id <= 3 ? id : id == 4 ? 1 : null });
    }
    for (int p = 1; p <= 3; p++) db.TimeSlots.Add(new() { TimeSlotID = p, StartTime = new TimeSpan(8, 30, 0) + TimeSpan.FromMinutes((p - 1) * 90), EndTime = new TimeSpan(9, 50, 0) + TimeSpan.FromMinutes((p - 1) * 90) });
    for (int id = 1; id <= 8; id++)
    {
        int section = id <= 6 ? (id - 1) % 3 + 1 : id - 3;
        int year = section <= 3 ? 4 : section == 4 ? 3 : 1;
        db.Subjects.Add(new() { SubjectID = id, SubjectName = "Subject " + id, StudyYearID = year, BranchID = section <= 3 ? section : section == 4 ? 1 : null, SemesterNumber = id <= 3 || id >= 7 ? 1 : 2,
            TheoreticalHours = 2, PracticalHours = 2, PracticalLectureCount = section == 1 ? 2 : 1 });
        db.FacultyMemberSubjects.Add(new() { SubjectID = id, FacultyMemberID = section });
    }
    await db.SaveChangesAsync();
    foreach (var subject in await db.Subjects.ToListAsync())
    {
        int section = subject.SubjectID <= 6 ? (subject.SubjectID - 1) % 3 + 1 : subject.SubjectID - 3;
        Schedule Row(string type, string? group, string day, int period) => new() { SubjectID = subject.SubjectID, FacultyMemberID = section, SectionID = section, StudyYearID = subject.StudyYearID,
            BranchID = subject.BranchID, SemesterNumber = subject.SemesterNumber, LectureType = type, GroupName = group, DayOfWeek = day, TimeSlotID = period, ClassroomID = type == "Theory" ? section + 10 : section };
        db.Schedules.Add(Row("Theory", null, "Thursday", 3));
        for (int n = 0; n < subject.EffectivePracticalLectureCount; n++)
        {
            db.Schedules.Add(Row("Practical", "A1", n == 0 ? "Sunday" : "Monday", 1));
            db.Schedules.Add(Row("Practical", "A2", n == 0 ? "Tuesday" : "Wednesday", 1));
        }
    }
    await db.SaveChangesAsync();
}
var original = await Rows();
var maintenance = new FourthYearPracticalMaintenance();
await using (var db = new AppDbContext()) await db.Classrooms.Where(c => c.ClassroomID == 1).ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacity, 15));
try { await maintenance.ApplyAsync(Backup("rejected-small-lab")); throw new Exception("Expected capacity rejection"); }
catch (ArgumentException e) { Check(e.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase), "Whole-section practical needs all 30 seats"); }
Check(Fingerprint(original) == Fingerprint(await Rows()), "Rejected conversion rolls back updates and deletions");
await using (var db = new AppDbContext())
{
    await db.Classrooms.Where(c => c.ClassroomID == 1).ExecuteUpdateAsync(s => s.SetProperty(c => c.Capacity, 30));
    await db.Schedules.Where(s => s.SubjectID == 1 && s.GroupName == "A2").ExecuteUpdateAsync(s => s.SetProperty(c => c.IsLocked, true));
}
try { await maintenance.ApplyAsync(Backup("rejected-lock")); throw new Exception("Expected lock rejection"); }
catch (InvalidOperationException) { Check((await Rows()).Count == original.Count, "Locked duplicate lectures are preserved"); }
await using (var db = new AppDbContext()) await db.Schedules.Where(s => s.IsLocked).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsLocked, false));
var result = await maintenance.ApplyAsync(Backup("before-conversion"));
var after = await Rows();
Check(result.ConvertedLectures == 8 && result.RemovedDuplicateLectures == 8 && File.Exists(result.SafetyBackupPath), "All three branches and both semesters converted after a safety snapshot");
Check(after.Where(s => s.StudyYearID == 4).All(s => s.GroupName == null), "Fourth year has no practical subgroups");
Check(Fingerprint(original.Where(s => s.StudyYearID != 4 || s.LectureType == "Theory")) == Fingerprint(after.Where(s => s.StudyYearID != 4 || s.LectureType == "Theory")), "Theory and other study years are unchanged");
Check(after.Where(s => s.StudyYearID == 4 && s.LectureType == "Practical").All(s => original.Any(o => o.ScheduleID == s.ScheduleID && o.DayOfWeek == s.DayOfWeek && o.TimeSlotID == s.TimeSlotID && o.FacultyMemberID == s.FacultyMemberID && o.ClassroomID == s.ClassroomID)), "Retained practical IDs, teachers, times and rooms are unchanged");
Check(after.Count(s => s.SubjectID == 1 && s.LectureType == "Practical") == 2, "Two genuine weekly practical occurrences remain two");
Check((await maintenance.ApplyAsync(Backup("repeat"))).ConvertedLectures == 0 && Fingerprint(after) == Fingerprint(await Rows()), "Repeating consolidation makes no changes");
var service = new ScheduleService();
var fourth = after.First(s => s.StudyYearID == 4 && s.LectureType == "Practical");
foreach (string invalid in new[] { "A1", "A2" })
{
    fourth.GroupName = invalid;
    try { await service.UpdateAsync(fourth); throw new Exception("Expected subgroup rejection"); }
    catch (ArgumentException e) { Check(e.Message.Contains("المرحلة الرابعة"), "Manual/API validation rejects " + invalid + " in fourth year"); }
}
fourth.GroupName = "a";
Check((await service.UpdateAsync(fourth)).GroupName == null, "Section A input is normalized to whole-section practical");
var rules = JsonSerializer.SerializeToElement(await new OverviewController().Rules(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
Check(rules.GetProperty("sections").EnumerateArray().Where(x => x.GetProperty("id").GetInt32() <= 3).All(x => x.GetProperty("groups").GetArrayLength() == 0), "Web rules expose no fourth-year subgroups");
var complete = await service.GenerateAsync();
Check(complete.AddedCount == 0, "Completion finds no missing or duplicate fourth-year practicals");
await service.RegenerateAsync(Backup("before-regeneration"));
var regenerated = await Rows();
Check(regenerated.Count(s => s.StudyYearID == 4 && s.LectureType == "Practical") == 8 && regenerated.Where(s => s.StudyYearID == 4).All(s => s.GroupName == null), "Regeneration produces whole-section practicals for every branch and semester");
Check(regenerated.Where(s => s.StudyYearID != 4 && s.LectureType == "Practical").Select(s => s.GroupName).Distinct().Order().SequenceEqual(new[] { "A1", "A2" }), "Other study years still generate both subgroups");
Exception? uiError = null;
var thread = new Thread(() =>
{
    try
    {
        ApplicationConfiguration.Initialize();
        using var form = new SchedulesForm();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = form.GetType();
        type.GetField("isUpdatingScheduleLookups", flags)!.SetValue(form, true);
        type.GetField("sectionsLookup", flags)!.SetValue(form, new List<Section> { new() { SectionID = 1, SectionName = "A", StudyYearID = 4, BranchID = 1 } });
        type.GetMethod("BindSectionsCombo", flags)!.Invoke(form, new object?[] { 4, 1, 1 });
        type.GetMethod("BindGroupNameCombo", flags)!.Invoke(form, new object?[] { "A2" });
        var combo = (ComboBox)type.GetField("cmbGroupName", flags)!.GetValue(form)!;
        Check(combo.Items.Count == 1 && combo.Items[0]?.ToString() == "A", "Desktop editor offers only A, replacing a stale A2 choice");
        var read = (Schedule)type.GetMethod("ReadScheduleFromForm", flags)!.Invoke(form, null)!;
        Check(read.GroupName == null, "Desktop editor saves the full section");
        var yearFilter = (ComboBox)type.GetField("cmbStudyYearFilter", flags)!.GetValue(form)!;
        var optionType = type.Assembly.GetType(type.Namespace + ".ComboOption")!;
        yearFilter.Items.Add(Activator.CreateInstance(optionType, new object?[] { 4, "Fourth Year" })!);
        yearFilter.SelectedIndex = 0;
        type.GetMethod("BindGroupFilterCombo", flags)!.Invoke(form, null);
        var groupFilter = (ComboBox)type.GetField("cmbGroupFilter", flags)!.GetValue(form)!;
        Check(!groupFilter.Enabled && groupFilter.Items.Count == 1, "Fourth-year filter offers no subgroup divisions");
        yearFilter.SelectedIndex = -1;
        type.GetMethod("BindGroupFilterCombo", flags)!.Invoke(form, null);
        Check(groupFilter.Enabled && groupFilter.Items.Count == 5, "All-years filter retains the other years' subgroups");
    }
    catch (Exception e) { uiError = e; }
});
thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
if (uiError is not null) throw uiError;
Console.WriteLine($"SUMMARY {checks} checks; 0 failures");
