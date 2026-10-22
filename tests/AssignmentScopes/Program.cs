using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.API;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

string database = "UniversityTimetable_AssignmentScopes_" + Guid.NewGuid().ToString("N")[..8];
var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = database };
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
Console.WriteLine("ISOLATED DATABASE " + database);
int checks = 0;
void Check(bool passed, string name) { if (!passed) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
async Task<List<Schedule>> Rows() { await using var db = new AppDbContext(); return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); }
string Json(object o) => JsonSerializer.Serialize(o);
var output = Path.Combine(Path.GetTempPath(), "AssignmentScopes", database); Directory.CreateDirectory(output);
await using (var db = new AppDbContext())
{
    await db.Database.EnsureCreatedAsync();
    db.StudyYears.AddRange(new StudyYear { StudyYearID = 1, YearName = "First Year" }, new StudyYear { StudyYearID = 3, YearName = "Third Year" }, new StudyYear { StudyYearID = 4, YearName = "Fourth Year" });
    db.Branches.AddRange(new Branch { BranchID = 1, BranchName = "Information Technology" }, new Branch { BranchID = 2, BranchName = "Cyber Security" });
    for (int id = 1; id <= 3; id++)
    {
        db.FacultyMembers.Add(new() { FacultyMemberID = id, FullName = "Teacher " + id });
        db.Classrooms.Add(new() { ClassroomID = id, ClassroomNumber = "Lab " + id, RoomType = "Lab", Capacity = 30 });
        db.Classrooms.Add(new() { ClassroomID = id + 10, ClassroomNumber = "Lecture " + id, RoomType = "Lecture", Capacity = 40 });
        db.TimeSlots.Add(new() { TimeSlotID = id, StartTime = new TimeSpan(8, 30, 0) + TimeSpan.FromMinutes((id - 1) * 90), EndTime = new TimeSpan(9, 50, 0) + TimeSpan.FromMinutes((id - 1) * 90) });
    }
    db.Sections.AddRange(new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 30 },
        new Section { SectionID = 2, SectionName = "B", StudyYearID = 1, StudentCount = 30 },
        new Section { SectionID = 3, SectionName = "A", StudyYearID = 3, BranchID = 1, StudentCount = 30 },
        new Section { SectionID = 4, SectionName = "A", StudyYearID = 3, BranchID = 2, StudentCount = 30 },
        new Section { SectionID = 5, SectionName = "A", StudyYearID = 4, BranchID = 1, StudentCount = 30 });
    db.Subjects.AddRange(new Subject { SubjectID = 1, SubjectName = "Common subject", StudyYearID = 1, SemesterNumber = 1, TheoreticalHours = 2, PracticalHours = 2 },
        new Subject { SubjectID = 2, SubjectName = "Specialized subject", StudyYearID = 3, BranchID = 1, SemesterNumber = 1, TheoreticalHours = 2 },
        new Subject { SubjectID = 3, SubjectName = "Fourth-year subject", StudyYearID = 4, BranchID = 1, SemesterNumber = 2, TheoreticalHours = 2, PracticalHours = 2 });
    db.FacultyMemberSubjects.Add(new() { FacultyMemberID = 3, SubjectID = 1 });
    await db.SaveChangesAsync();
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE FacultyMemberSubjects DROP CONSTRAINT FK_FacultyMemberSubjects_Sections_SectionID; DROP INDEX IX_FacultyMemberSubjects_SectionID ON FacultyMemberSubjects; ALTER TABLE FacultyMemberSubjects DROP COLUMN SectionID;");
    await FacultyAssignmentScopeSchema.EnsureAsync(db);
    await FacultyAssignmentScopeSchema.EnsureAsync(db);
    var legacy = await db.FacultyMemberSubjects.AsNoTracking().SingleAsync();
    Check(legacy.FacultyMemberID == 3 && legacy.SectionID == null, "Existing database upgrades idempotently and preserves legacy assignment scope");
}
var assignments = new FacultyMemberSubjectService();
await assignments.DeleteAsync(3, 1);
await assignments.AddAsync(new() { FacultyMemberID = 1, SubjectID = 1, SectionID = 1 });
await assignments.AddAsync(new() { FacultyMemberID = 2, SubjectID = 1, SectionID = 2 });
await assignments.AddAsync(new() { FacultyMemberID = 3, SubjectID = 2, SectionID = 3 });
await assignments.AddAsync(new() { FacultyMemberID = 1, SubjectID = 3, SectionID = 5 });
await Reject(() => assignments.AddAsync(new() { FacultyMemberID = 2, SubjectID = 2, SectionID = 4 }), "Assignment rejects a section in another specialization");
await Reject(() => assignments.AddAsync(new() { FacultyMemberID = 2, SubjectID = 3, SectionID = 3 }), "Assignment rejects a section in another study year");
await Reject(() => assignments.AddAsync(new() { FacultyMemberID = 2, SubjectID = 2, SectionID = 999 }), "Assignment rejects an unknown section");
var dto = AssignmentDto.From((await assignments.GetAllAsync()).First(a => a.SubjectID == 2));
Check(dto.StudyYearID == 3 && dto.BranchID == 1 && dto.ToEntity().SectionID == 3, "API returns the derived year/specialization and persists the selected section");
var service = new ScheduleService();
await service.GenerateAsync();
var generated = await Rows();
Check(generated.Count == 9 && generated.Where(s => s.SubjectID == 1 && s.SectionID == 1).All(s => s.FacultyMemberID == 1) &&
    generated.Where(s => s.SubjectID == 1 && s.SectionID == 2).All(s => s.FacultyMemberID == 2), "Generation assigns teacher 1 to A and teacher 2 to B for theory and practical");
Check(generated.Where(s => s.StudyYearID == 4).All(s => s.GroupName == null), "Scoped fourth-year assignment still uses whole-section practical");
var wrong = JsonSerializer.Deserialize<Schedule>(Json(generated.First(s => s.SubjectID == 1 && s.SectionID == 1)))!;
wrong.FacultyMemberID = 2;
await Reject(() => service.UpdateAsync(wrong), "Manual/API schedule update cannot use a teacher assigned to another section");
await Reject(() => assignments.UpdateAsync(1, 1, new() { FacultyMemberID = 1, SubjectID = 1, SectionID = 2 }), "Narrowing/moving scope cannot orphan saved lessons");
Check(Json(generated) == Json(await Rows()), "Rejected changes preserve all timetable fields");
await assignments.UpdateAsync(1, 1, new() { FacultyMemberID = 1, SubjectID = 1 });
Check((await assignments.GetByIdAsync(1, 1))!.SectionID == null, "The same teacher may explicitly cover all matching sections");
await assignments.UpdateAsync(1, 1, new() { FacultyMemberID = 1, SubjectID = 1, SectionID = 1 });
var alternatives = await new TimetableAlternativesService().GenerateAsync(new(1, 1, 5));
Check(alternatives.Alternatives.All(a => a.Lectures.Where(l => l.Subject == "Common subject").All(l =>
    l.Section.EndsWith(" / A") ? l.Faculty == "Teacher 1" : l.Faculty == "Teacher 2")), "Alternative search retains section-specific teachers");
var backups = new TimetableBackupService();
string backup = Path.Combine(output, "scoped.uttbackup.json");
await backups.SaveAsync(backup);
Check((await backups.PreviewRestoreAsync(backup)).CanRestore, "Backups round-trip with scoped assignments");
var scopedAssignments = await assignments.GetAllAsync();
await using (var db = new AppDbContext()) await db.FacultyMemberSubjects.ExecuteUpdateAsync(s => s.SetProperty(a => a.SectionID, (int?)null));
string legacyBackup = Path.Combine(output, "legacy.uttbackup.json");
await backups.SaveAsync(legacyBackup);
var legacyNode = JsonNode.Parse(await File.ReadAllTextAsync(legacyBackup))!;
foreach (var reference in legacyNode["Payload"]!["References"]!.AsArray())
    if (reference!["Table"]!.GetValue<string>() == "FacultyMemberSubjects")
    {
        var values = JsonNode.Parse(reference["Values"]!.GetValue<string>())!;
        values.AsObject().Remove("SectionID"); reference["Values"] = values.ToJsonString();
    }
var payloadType = typeof(TimetableBackupService).Assembly.GetType(typeof(TimetableBackupService).Namespace + ".TimetableBackupPayload")!;
var payload = JsonSerializer.Deserialize(legacyNode["Payload"]!.ToJsonString(), payloadType)!;
legacyNode["Checksum"] = (string)typeof(TimetableBackupService).GetMethod("Checksum", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { payload })!;
await File.WriteAllTextAsync(legacyBackup, legacyNode.ToJsonString());
Check((await backups.PreviewRestoreAsync(legacyBackup)).CanRestore, "Backups predating scopes still restore when assignments remain unrestricted");
await using (var db = new AppDbContext())
    foreach (var assignment in scopedAssignments)
        await db.FacultyMemberSubjects.Where(a => a.FacultyMemberID == assignment.FacultyMemberID && a.SubjectID == assignment.SubjectID)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.SectionID, assignment.SectionID));
Check(!(await backups.PreviewRestoreAsync(legacyBackup)).CanRestore, "An old unrestricted backup does not bypass a newly restricted assignment");
await using (var db = new AppDbContext()) await db.Schedules.ExecuteDeleteAsync();
await assignments.DeleteAsync(2, 1);
await Reject(() => service.GenerateAsync(), "Generation reports the uncovered B section instead of silently skipping it");
Check((await Rows()).Count == 0, "Incomplete coverage does not save a partial timetable");
await assignments.AddAsync(new() { FacultyMemberID = 2, SubjectID = 1, SectionID = 2 });

ExcelWorkbookData Workbook(params string[] columns)
{
    var table = new DataTable("FacultyMemberSubjects"); foreach (string name in columns) table.Columns.Add(name);
    table.Rows.Add(columns.Select(c => c switch { "FacultyMemberID" => "1", "SubjectID" => "1", "SectionID" => "2", _ => "" }).ToArray());
    var book = new ExcelWorkbookData(); book.Sheets.Add(new("FacultyMemberSubjects", table, new[] { 2 })); return book;
}
var import = new ExcelImportService();
var omitted = await import.ValidateAsync(ExcelImportMode.AcademicData, Workbook("FacultyMemberID", "SubjectID").Sheets, updateExisting: true);
Check(omitted.IsValid && omitted.Changes.All(c => c.Action == "Unchanged"), "Old Excel files without SectionID preserve the existing scope");
var update = await import.ValidateAsync(ExcelImportMode.AcademicData, Workbook("FacultyMemberID", "SubjectID", "SectionID").Sheets, updateExisting: true);
Check(update.IsValid, "Excel previews a scoped assignment update");
await import.SaveAsync(update, true);
Check((await assignments.GetByIdAsync(1, 1))!.SectionID == 2, "Excel updates the scope instead of inserting a duplicate key");
await assignments.UpdateAsync(1, 1, new() { FacultyMemberID = 1, SubjectID = 1, SectionID = 1 });
await using (var db = new AppDbContext())
{
    var subject = await db.Subjects.AsNoTracking().SingleAsync(s => s.SubjectID == 2); subject.BranchID = 2;
    await Reject(() => new SubjectService().UpdateAsync(subject), "Changing a subject specialization cannot invalidate scoped assignments");
    var section = await db.Sections.AsNoTracking().SingleAsync(s => s.SectionID == 3); section.BranchID = 2;
    await Reject(() => new SectionService().UpdateAsync(section), "Changing a section specialization cannot invalidate scoped assignments");
}

var uiAssignments = await assignments.GetAllAsync();
Exception? uiError = null;
var thread = new Thread(() =>
{
    try
    {
        ApplicationConfiguration.Initialize();
        using var form = new FacultyMemberSubjectsForm();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = form.GetType();
        void Set(string field, object value) => type.GetField(field, flags)!.SetValue(form, value);
        object? Call(string method, params object?[] values) => type.GetMethod(method, flags)!.Invoke(form, values);
        ComboBox Combo(string field) => (ComboBox)type.GetField(field, flags)!.GetValue(form)!;
        using var db = new AppDbContext();
        Set("assignmentSubjects", db.Subjects.AsNoTracking().ToList()); Set("assignmentSections", db.Sections.AsNoTracking().ToList()); Set("assignmentBranches", db.Branches.AsNoTracking().ToList());
        Set("bindingAssignmentScope", true);
        var year = Combo("cmbAssignmentStudyYear"); var option = type.Assembly.GetType(type.Namespace + ".ComboOption")!;
        year.Items.Add(Activator.CreateInstance(option, new object?[] { 1, "First Year" })!);
        year.Items.Add(Activator.CreateInstance(option, new object?[] { 3, "Third Year" })!);
        year.Items.Add(Activator.CreateInstance(option, new object?[] { 4, "Fourth Year" })!);
        year.SelectedIndex = 0; Call("BindAssignmentBranches", (object?)null); Call("BindAssignmentSubjects", 1); Call("BindAssignmentSections", 2, true);
        Check(!Combo("cmbAssignmentBranch").Enabled && Combo("cmbSubject").Items.Count == 1 && Combo("cmbAssignmentSection").Items.Count == 3,
            "Desktop year filter shows general subjects and A/B scope choices");
        var semester = Combo("cmbAssignmentSemester");
        Check(semester.Items.Count == 3 && semester.SelectedIndex == 0, "Semester selector defaults to all semesters");
        semester.SelectedIndex = 2; Call("BindAssignmentSubjects", (object?)null);
        Check(Combo("cmbSubject").Items.Count == 0, "Second-semester filter excludes first-semester subjects");
        semester.SelectedIndex = 1; Call("BindAssignmentSubjects", 1);
        Check(Combo("cmbSubject").Items.Count == 1, "First-semester filter includes matching subjects");
        semester.SelectedIndex = 0;
        var rowType = type.GetNestedType("AssignmentRow", BindingFlags.NonPublic)!;
        var rows = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType))!;
        foreach (var assignment in uiAssignments)
            rows.Add(rowType.GetMethod("FromAssignment")!.Invoke(null, new object[] { assignment }));
        Set("assignmentRows", rows); Call("ApplyAssignmentRowsFilter");
        var grid = (DataGridView)type.GetField("dgvFacultyMemberSubjects", flags)!.GetValue(form)!;
        int allRows = grid.Rows.Count;
        semester.SelectedIndex = 2; Call("ApplyAssignmentRowsFilter");
        Check(grid.Columns.Cast<DataGridViewColumn>().Any(c => c.DataPropertyName == "SemesterName") && grid.Rows.Count > 0 &&
            grid.Rows.Cast<DataGridViewRow>().All(r => (int)r.DataBoundItem.GetType().GetProperty("SemesterNumber")!.GetValue(r.DataBoundItem)! == 2),
            "Assignment table displays the semester column and filters to second semester");
        semester.SelectedIndex = 0; Call("ApplyAssignmentRowsFilter");
        Check(grid.Rows.Count == allRows, "All-semesters choice restores every assignment");
        year.SelectedIndex = 1; Call("BindAssignmentBranches", 1); Call("BindAssignmentSubjects", 2); Call("BindAssignmentSections", null, false);
        Check(Combo("cmbAssignmentBranch").Enabled && Combo("cmbSubject").Items.Count == 1 && Combo("cmbAssignmentSection").SelectedValue?.ToString() == "3",
            "Desktop specialization filters subjects and selects the sole matching section");
        year.SelectedIndex = 2; Call("BindAssignmentBranches", 1); Call("BindAssignmentSubjects", 3); Call("BindAssignmentSections", null, false);
        Check(Combo("cmbAssignmentSection").Items.Cast<object>().All(o => !o.ToString()!.Contains("A1") && !o.ToString()!.Contains("A2")), "Fourth-year assignment editor has no practical subgroup choices");
        Check(Combo("cmbAssignmentLectureType").Items.Cast<object>().Select(o=>o.ToString()).SequenceEqual(new[] { "كلاهما", "نظري", "عملي" }),
            "Desktop assignment editor offers both, theory and practical components");
        Combo("cmbAssignmentLectureType").SelectedIndex = 2;
        Call("ClearAssignmentForm");
        Check(Combo("cmbAssignmentLectureType").SelectedIndex == 0, "Clearing the editor resets the teaching component");
        Check(year.SelectedIndex == -1 && Combo("cmbSubject").Items.Count == 0 && Combo("cmbAssignmentSection").Items.Count == 0,
            "Clearing the editor resets the scope and dependent choices");
        form.Size = new Size(1180, 720); form.PerformLayout();
        Directory.CreateDirectory("output/assignment-scopes");
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save("output/assignment-scopes/editor.png");
    }
    catch (Exception e) { uiError = e; }
});
thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
if (uiError is not null) throw uiError;
Console.WriteLine($"SUMMARY {checks} checks; 0 failures");
