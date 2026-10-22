using System.Reflection;
using System.Text.Json;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class Program
{
    static Func<AppDbContext> factory = null!;
    static FacultyScheduleEditorService service = null!;
    static List<Schedule> initial = [];
    static int failures;
    static readonly List<object> results = [];
    static readonly Fault fault = new();
    static readonly string output = Path.GetFullPath("output/faculty-editor-verification");
    static Schedule Copy(Schedule s) => JsonSerializer.Deserialize<Schedule>(JsonSerializer.Serialize(s))!;
    static async Task<List<Schedule>> Rows() { await using var db = factory(); return await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); }
    static string Json(object value) => JsonSerializer.Serialize(value);
    static void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    static async Task Reject(Func<Task> action) { try { await action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or OperationCanceledException) { return; } throw new Exception("Expected rejection"); }
    static async Task Reset()
    {
        fault.Enabled = false;
        await using var db = factory();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var originalIds = initial.Select(s => s.ScheduleID).ToArray();
        await db.Schedules.Where(s => !originalIds.Contains(s.ScheduleID)).ExecuteDeleteAsync();
        await FacultyScheduleWriter.ReplacePlacementsAsync(db, initial.Select(Copy).ToList());
        await tx.CommitAsync();
    }
    static async Task Check(string name, Func<Task> action)
    {
        try { await Reset(); await action(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; results.Add(new { name, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e.ToString()); }
        finally { fault.Enabled = false; }
    }
    static FacultyScheduleRequest Request(int faculty = 1, FacultyAvailabilityMode mode = FacultyAvailabilityMode.Strict, params string[] days)
        => new(faculty, 1, mode, days.Length == 0 ? ["Sunday"] : days, TimeLimitSeconds: 5);
    [STAThread]
    static void Main(string[] args)
    {
        Directory.CreateDirectory(output);
        Run().GetAwaiter().GetResult();
        if (args.Contains("ui") && failures == 0) Ui();
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { Count = results.Count, Failures = failures, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY: {results.Count} checks, {failures} failures"); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    static async Task Run()
    {
        string database = "UniversityTimetable_FacultyEditor_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost", InitialCatalog = database, IntegratedSecurity = true, Encrypt = false, TrustServerCertificate = true };
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).AddInterceptors(fault).Options;
        factory = () => new AppDbContext(options); service = new(factory);
        File.WriteAllText(Path.Combine(output, "isolated-database.txt"), database);
        await using (var db = factory())
        {
            await db.Database.EnsureCreatedAsync();
            db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
            db.FacultyMembers.AddRange(new FacultyMember { FacultyMemberID = 1, FullName = "د. أحمد محمد" }, new FacultyMember { FacultyMemberID = 2, FullName = "د. سارة علي" }, new FacultyMember { FacultyMemberID = 3, FullName = "Other Teacher" });
            db.Sections.AddRange(new Section { SectionID = 1, SectionName = "A", StudentCount = 40, StudyYearID = 1 }, new Section { SectionID = 2, SectionName = "B", StudentCount = 40, StudyYearID = 1 });
            db.Subjects.AddRange(Subject(1,"Programming Lab",0,2), Subject(2,"Networks Lab",0,2), Subject(3,"Mathematics",2,0), Subject(4,"English",2,0));
            db.Classrooms.AddRange(new Classroom { ClassroomID = 1, ClassroomNumber = "Room 101", Capacity = 60, RoomType = "Lecture" }, new Classroom { ClassroomID = 2, ClassroomNumber = "Lab 1", Capacity = 30, RoomType = "Lab" }, new Classroom { ClassroomID = 3, ClassroomNumber = "Lab 2", Capacity = 30, RoomType = "Lab" }, new Classroom { ClassroomID = 4, ClassroomNumber = "Tiny", Capacity = 5, RoomType = "Lecture" });
            db.TimeSlots.AddRange(new TimeSlot { TimeSlotID = 1, StartTime = new(8,30,0), EndTime = new(10,0,0) }, new TimeSlot { TimeSlotID = 2, StartTime = new(10,10,0), EndTime = new(11,40,0) }, new TimeSlot { TimeSlotID = 3, StartTime = new(11,50,0), EndTime = new(13,20,0) });
            db.FacultyMemberSubjects.AddRange(new FacultyMemberSubject { FacultyMemberID = 1, SubjectID = 1 }, new FacultyMemberSubject { FacultyMemberID = 2, SubjectID = 2 }, new FacultyMemberSubject { FacultyMemberID = 1, SubjectID = 3 }, new FacultyMemberSubject { FacultyMemberID = 3, SubjectID = 4 });
            await db.SaveChangesAsync();
            db.Schedules.AddRange(Lecture(1,1,2,1,"Sunday","Practical","A1",1), Lecture(1,1,2,2,"Sunday","Practical","A2",1), Lecture(2,2,3,1,"Sunday","Practical","A2",1), Lecture(2,2,3,2,"Sunday","Practical","A1",1), Lecture(3,1,1,3,"Tuesday","Theory",null,2), Lecture(4,3,1,1,"Wednesday","Theory",null,2));
            await db.SaveChangesAsync();
        }
        initial = await Rows();
        await Check("Configured teaching days, periods and Arabic faculty load dynamically", async () =>
        {
            var data = await service.GetLookupsAsync(); Assert(data.Days.Count > 0 && data.Periods.Count == 3 && data.Faculty.Any(f => f.Name.Contains("أحمد")));
            Assert((await service.GetCurrentAsync(1,1)).Count == 3);
        });
        await Check("Slot inspection and preview never write to the database", async () =>
        {
            var before = Json(await Rows()); var slots = await service.PreviewAsync(Request(), true);
            Assert(!slots.CanSave && slots.AvailableSlots.Count == 3);
            var proposal = await service.PreviewAsync(Request()); Assert(proposal.CanSave, proposal.Message);
            Assert(before == Json(await Rows()));
        });
        await Check("Strict single day preserves exchange, identities and every unrelated lecture", async () =>
        {
            var before = await Rows(); var proposal = await service.PreviewAsync(Request());
            Assert(proposal.CanSave && proposal.Changes.Count == 3 && proposal.Changes.All(c => c.NewDay == "Sunday"));
            Assert(proposal.Changes.Count(c => c.Changed) == 1);
            await Reject(() => service.SaveAsync(proposal, false)); Assert(Json(before) == Json(await Rows()));
            Assert(await service.SaveAsync(proposal, true) == 1);
            var after = await Rows(); Assert(after.Count == before.Count && after.All(s => !s.IsLocked));
            Assert(Json(before.Where(s => s.FacultyMemberID != 1)) == Json(after.Where(s => s.FacultyMemberID != 1)));
            foreach (var old in before) { var saved = after.Single(s => s.ScheduleID == old.ScheduleID); old.DayOfWeek = saved.DayOfWeek; old.TimeSlotID = saved.TimeSlotID; old.ClassroomID = saved.ClassroomID; Assert(Json(old) == Json(saved)); }
        });
        await Check("Multiple selected days minimize changes to zero", async () =>
        {
            var proposal = await service.PreviewAsync(Request(1,FacultyAvailabilityMode.Strict,"Sunday","Tuesday"));
            Assert(proposal.CanSave && proposal.Changes.All(c => !c.Changed)); Assert(await service.SaveAsync(proposal,true) == 0);
        });
        await Check("Strict unavailable exchange day fails without changing any other teacher", async () =>
        {
            var before = Json(await Rows()); var proposal = await service.PreviewAsync(Request(1,FacultyAvailabilityMode.Strict,"Tuesday"));
            Assert(!proposal.CanSave && proposal.Status == "Infeasible", proposal.Status); await Reject(() => service.SaveAsync(proposal,true)); Assert(before == Json(await Rows()));
        });
        await Check("Preferred mode tries selected days first and explicitly reports fallback", async () =>
        {
            var proposal = await service.PreviewAsync(Request(1,FacultyAvailabilityMode.Preferred,"Tuesday"));
            Assert(proposal.CanSave && proposal.Changes.Count(c => c.OutsidePreferredDays) == 2, proposal.Message);
            Assert(proposal.Suggestions.Any(s => s.Contains("ثبت عدم"))); Assert(proposal.Changes.All(c => !c.Changed));
        });
        await Check("Unavailable days remain hard constraints in preferred mode", async () =>
        {
            var proposal = await service.PreviewAsync(Request(1,FacultyAvailabilityMode.Preferred,"Tuesday") with { UnavailableDays = new[] { "Sunday" } }); Assert(!proposal.CanSave);
        });
        await Check("Period preferences apply after day compliance and minimal moves", async () =>
        {
            var proposal = await service.PreviewAsync(Request(3,FacultyAvailabilityMode.Strict,"Tuesday") with { PreferredPeriodIds = new[] { 2 } });
            Assert(proposal.CanSave && proposal.Changes.Single().NewPeriod.StartsWith("10:10"), proposal.Message);
            await service.SaveAsync(proposal,true); var saved = (await Rows()).Single(s => s.FacultyMemberID == 3); Assert(saved.TimeSlotID == 2 && saved.ClassroomID != 4);
        });
        await Check("Student and room conflicts are avoided while unsuitable rooms stay excluded", async () =>
        {
            var proposal = await service.PreviewAsync(Request(3,FacultyAvailabilityMode.Strict,"Tuesday")); Assert(proposal.CanSave);
            await service.SaveAsync(proposal,true); var saved = (await Rows()).Single(s => s.FacultyMemberID == 3); Assert(saved.TimeSlotID != 3 && saved.ClassroomID != 4);
        });
        await Check("Ambiguous or empty preferences are rejected without writes", async () =>
        {
            string before = Json(await Rows()); await Reject(() => service.PreviewAsync(Request() with { SelectedDays = Array.Empty<string>() }));
            await Reject(() => service.PreviewAsync(Request() with { UnavailableDays = new[] { "Sunday" } })); Assert(before == Json(await Rows()));
        });
        await Check("Resource changes after preview invalidate the save", async () =>
        {
            var proposal = await service.PreviewAsync(Request());
            await using(var db = factory()) await db.Classrooms.Where(r => r.ClassroomID == 1).ExecuteUpdateAsync(set => set.SetProperty(r => r.ClassroomNumber,"Renamed"));
            try { string before = Json(await Rows()); await Reject(() => service.SaveAsync(proposal,true)); Assert(before == Json(await Rows())); }
            finally { await using var db = factory(); await db.Classrooms.Where(r => r.ClassroomID == 1).ExecuteUpdateAsync(set => set.SetProperty(r => r.ClassroomNumber,"Room 101")); }
        });
        await Check("Unrelated schedule changes after preview invalidate the save", async () =>
        {
            var proposal = await service.PreviewAsync(Request());
            await using(var db = factory()) await db.Schedules.Where(s => s.FacultyMemberID == 3).ExecuteUpdateAsync(set => set.SetProperty(s => s.DayOfWeek,"Thursday"));
            string before = Json(await Rows()); await Reject(() => service.SaveAsync(proposal,true)); Assert(before == Json(await Rows()));
        });
        await Check("Canceled search and save make no partial writes", async () =>
        {
            string before = Json(await Rows()); using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Reject(() => service.PreviewAsync(Request(),token:cancel.Token));
            var proposal = await service.PreviewAsync(Request()); await Reject(() => service.SaveAsync(proposal,true,cancel.Token)); Assert(before == Json(await Rows()));
        });
        await Check("Failure after deleting changed rows rolls back all rows and IDs", async () =>
        {
            string before = Json(await Rows()); var proposal = await service.PreviewAsync(Request()); fault.Enabled = true;
            try { await Reject(() => service.SaveAsync(proposal,true)); } finally { fault.Enabled = false; }
            Assert(before == Json(await Rows()));
        });
        await Check("Atomic writer permits cyclic swaps without changing IDs", async () =>
        {
            var pair = initial.Where(s => s.LectureType == "Practical").Select(Copy).ToList(); foreach(var lecture in pair) lecture.TimeSlotID = lecture.TimeSlotID == 1 ? 2 : 1;
            await using var db = factory(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await FacultyScheduleWriter.ReplacePlacementsAsync(db,pair);
            var saved = await db.Schedules.AsNoTracking().OrderBy(s => s.ScheduleID).ToListAsync(); Assert(pair.All(p => saved.Single(s => s.ScheduleID == p.ScheduleID).TimeSlotID == p.TimeSlotID));
            await tx.RollbackAsync(); Assert(Json(initial) == Json(await Rows()));
        });
        await Check("Preferred mode moves an outside lecture inside selected days before considering fallback", async () =>
        {
            var proposal = await service.PreviewAsync(Request(3,FacultyAvailabilityMode.Preferred,"Tuesday"));
            Assert(proposal.CanSave && proposal.Changes.All(c => c.Changed && !c.OutsidePreferredDays));
            Assert(!proposal.Suggestions.Any(s => s.Contains("ثبت عدم")));
        });
        await Check("Empty faculty scope and unknown faculty cannot produce a saveable proposal", async () =>
        {
            await Reject(() => service.PreviewAsync(Request(999)));
            await Reject(() => service.PreviewAsync(Request() with { Semester = 2 }));
        });
        await Check("Missing practical counterpart is rejected without modifying remaining rows", async () =>
        {
            int id = initial.Single(s => s.SubjectID == 2 && s.GroupName == "A1").ScheduleID;
            await using(var db = factory()) await db.Schedules.Where(s => s.ScheduleID == id).ExecuteUpdateAsync(set => set.SetProperty(s => s.GroupName,"A2").SetProperty(s => s.DayOfWeek,"Thursday"));
            try { string before = Json(await Rows()); await Reject(() => service.PreviewAsync(Request())); Assert(before == Json(await Rows())); }
            finally { await using var db = factory(); await db.Schedules.Where(s => s.ScheduleID == id).ExecuteUpdateAsync(set => set.SetProperty(s => s.GroupName,"A1").SetProperty(s => s.DayOfWeek,"Sunday")); }
        });
        await Check("Whole-section practical remains whole and keeps duration and classroom suitability", async () =>
        {
            await using(var db = factory())
            {
                db.Branches.Add(new() { BranchID = 8, BranchName = "Computer Science" });
                db.StudyYears.Add(new() { StudyYearID = 4, YearName = "Fourth Year" });
                db.Sections.Add(new() { SectionID = 4, SectionName = "A", StudentCount = 30, StudyYearID = 4, BranchID = 8 });
                db.Subjects.Add(new() { SubjectID = 8, SubjectName = "Whole-section lab", StudyYearID = 4, BranchID = 8, SemesterNumber = 1, PracticalHours = 2, CreditUnits = 2, RequirementType = "Core" });
                db.FacultyMemberSubjects.Add(new() { FacultyMemberID = 3, SubjectID = 8 }); await db.SaveChangesAsync();
                var whole = Lecture(8,3,2,2,"Wednesday","Practical",null,4); whole.StudyYearID = 4; whole.BranchID = 8; db.Schedules.Add(whole); await db.SaveChangesAsync();
            }
            try
            {
                var proposal = await service.PreviewAsync(Request(3,FacultyAvailabilityMode.Strict,"Tuesday")); Assert(proposal.CanSave,proposal.Message);
                await service.SaveAsync(proposal,true); var saved = (await Rows()).Single(s => s.SubjectID == 8); Assert(saved.GroupName is null && saved.LectureType == "Practical" && saved.ClassroomID is 2 or 3);
            }
            finally
            {
                await using var db = factory(); await db.Schedules.Where(s => s.SubjectID == 8).ExecuteDeleteAsync(); await db.FacultyMemberSubjects.Where(s => s.SubjectID == 8).ExecuteDeleteAsync();
                await db.Subjects.Where(s => s.SubjectID == 8).ExecuteDeleteAsync(); await db.Sections.Where(s => s.SectionID == 4).ExecuteDeleteAsync(); await db.StudyYears.Where(s => s.StudyYearID == 4).ExecuteDeleteAsync(); await db.Branches.Where(s => s.BranchID == 8).ExecuteDeleteAsync();
            }
        });
        await Check("Periods with a different duration cannot replace the saved lecture period", async () =>
        {
            await using(var db = factory()) await db.TimeSlots.Where(p => p.TimeSlotID == 2).ExecuteUpdateAsync(set => set.SetProperty(p => p.EndTime,new TimeSpan(11,30,0)));
            try
            {
                var proposal = await service.PreviewAsync(Request(3,FacultyAvailabilityMode.Strict,"Tuesday") with { PreferredPeriodIds = new[] { 2 } }); Assert(proposal.CanSave);
                await service.SaveAsync(proposal,true); Assert((await Rows()).Single(s => s.FacultyMemberID == 3).TimeSlotID != 2);
            }
            finally { await using var db = factory(); await db.TimeSlots.Where(p => p.TimeSlotID == 2).ExecuteUpdateAsync(set => set.SetProperty(p => p.EndTime,new TimeSpan(11,40,0))); }
        });
        await Check("Existing general generation retains the valid fixture without moving lectures", async () =>
        {
            var before = await Rows(); await new ScheduleService().GenerateAsync(); var after = await Rows(); Assert(before.All(old => Json(old) == Json(after.Single(s => s.ScheduleID == old.ScheduleID))));
        });
        await Reset();
    }
    static Subject Subject(int id,string name,int theory,int practical) => new() { SubjectID = id, SubjectName = name, StudyYearID = 1, SemesterNumber = 1, TheoreticalHours = theory, PracticalHours = practical, CreditUnits = 2, RequirementType = "Core" };
    static Schedule Lecture(int subject,int faculty,int room,int period,string day,string type,string? group,int section) => new() { SubjectID = subject, FacultyMemberID = faculty, ClassroomID = room, TimeSlotID = period, DayOfWeek = day, LectureType = type, GroupName = group, SectionID = section, StudyYearID = 1, SemesterNumber = 1 };
    sealed class Fault : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { if (Enabled) throw new InvalidOperationException("Injected write failure"); return base.SavingChangesAsync(data,result,cancellationToken); }
    }
    static IEnumerable<Control> Descendants(Control root) { foreach(Control child in root.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; } }
    static async Task WaitUi(Func<bool> done)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!done()) { await Task.Delay(20); if (timer.Elapsed.TotalSeconds > 30) throw new Exception("UI load timed out"); }
    }
    static void Ui()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        using var form = new FacultyScheduleEditorForm(service);
        form.Shown += async (_, _) =>
        {
            try
            {
                var controls = Descendants(form).Where(c => c.Name.Length > 0).GroupBy(c => c.Name).ToDictionary(g => g.Key,g => g.First());
                var faculty = (ComboBox)controls["facultySelection"]; var save = (Button)controls["saveProposal"];
                await WaitUi(() => faculty.Enabled && faculty.Items.Count == 3);
                Assert(!save.Enabled);
                faculty.SelectedItem = faculty.Items.Cast<FacultyEditorOption>().Single(x => x.Id == 1);
                await WaitUi(() => faculty.Enabled && ((DataGridView)controls["currentTimetable"]).Rows.Count == 3);
                var days = (CheckedListBox)controls["preferredDays"];
                for(int i=0;i<days.Items.Count;i++) days.SetItemChecked(i,i==0);
                ((ComboBox)controls["availabilityMode"]).SelectedIndex = 0;
                ((Button)controls["generateProposal"]).PerformClick();
                var proposedGrid = (DataGridView)controls["proposedTimetable"];
                await WaitUi(() => save.Enabled && proposedGrid.Visible && proposedGrid.Width > 0 && proposedGrid.Height > 0);
                Assert(proposedGrid.Rows.Count == 3 && proposedGrid.Columns.Count == 13,"Proposal grid must display lecture rows");
                foreach(var size in new[] { new System.Drawing.Size(800,600), new System.Drawing.Size(1180,820) })
                {
                    form.Size = size; await Task.Delay(100);
                    foreach(var name in new[] {"saveProposal","generateProposal","closeEditor"})
                    { var c=controls[name]; var rectangle=form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle)); Assert(rectangle.Top >=0 && rectangle.Bottom <= form.ClientSize.Height && rectangle.Left >=0 && rectangle.Right <= form.ClientSize.Width,"Action clipped: " + name); }
                    Assert(proposedGrid.Width > 0 && proposedGrid.Height > 0,"Proposal grid must remain visible after resizing");
                    using var bitmap = new System.Drawing.Bitmap(form.Width,form.Height); form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height));
                    using(var gridBitmap = new System.Drawing.Bitmap(proposedGrid.Width,proposedGrid.Height))
                    {
                        proposedGrid.DrawToBitmap(gridBitmap,new System.Drawing.Rectangle(0,0,gridBitmap.Width,gridBitmap.Height));
                        gridBitmap.Save(Path.Combine(output,$"proposal-grid-{size.Width}.png"));

                    }
                    bitmap.Save(Path.Combine(output,$"editor-{size.Width}.png"));
                }
                Assert(Json(initial) == Json(await Rows()),"UI preview wrote data");
                using var schedule = new SchedulesForm();
                var grid = Descendants(schedule).OfType<DataGridView>().First(g => g.Name == "dgvSchedules");
                Assert(!grid.Columns.Cast<DataGridViewColumn>().Any(c => c.Name == "colIsLocked" || c.HeaderText == "Locked")); Assert(grid.ContextMenuStrip is null);
                results.Add(new { name="Arabic editor previews at 800x600 and 1180x820; actions and rows visible, no writes, no Locked column/menu", passed=true }); Console.WriteLine("PASS UI and removed Locked controls");
            }
            catch(Exception e) { failures++; results.Add(new {name="UI",passed=false,error=e.ToString()}); Console.WriteLine("FAIL UI: " + e.Message); }
            finally { form.Close(); }
        };
        Application.Run(form);
    }
}
