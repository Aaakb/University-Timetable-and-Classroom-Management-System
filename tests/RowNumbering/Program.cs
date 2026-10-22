using System.ComponentModel;
using System.Data;
using System.Reflection;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.Models;

internal static class RowNumberingChecks
{
    private static int checks;
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        // Skip OnLoad/database work. The same runtime grid configuration is invoked explicitly below.
        var oldContext = LicenseManager.CurrentContext;
        LicenseManager.CurrentContext = new DesignContext();
        try
        {
            foreach (var item in new (Func<UserControl> Create, string Grid, string Configure)[]
            {
                (() => new BranchesForm(), "dgvBranches", "ConfigureBranchesGrid"),
                (() => new StudyYearsForm(), "dgvStudyYears", "ConfigureStudyYearsGrid"),
                (() => new SectionsForm(), "dgvSections", "ConfigureSectionsGrid"),
                (() => new SubjectsForm(), "dgvSubjects", "ConfigureSubjectsGrid"),
                (() => new ClassroomsForm(), "dgvClassrooms", "ConfigureClassroomsGrid"),
                (() => new TimeSlotsForm(), "dgvTimeSlots", "ConfigureTimeSlotsGrid"),
                (() => new FacultyMembersForm(), "dgvFacultyMembers", "ConfigureFacultyMembersGrid"),
                (() => new FacultyMemberSubjectsForm(), "dgvFacultyMemberSubjects", "ConfigureAssignmentsGrid"),
                (() => new SchedulesForm(), "dgvSchedules", "ConfigureScheduleGrid")
            })
            {
                using var page = item.Create();
                Invoke(page, item.Configure);
                var grid = (DataGridView)page.Controls.Find(item.Grid, true).Single();
                string name = page.GetType().Name;
                Check(name + " uses a static unbound number column", grid.Columns[0].Name == "colRowNumber" &&
                    grid.Columns[0].DataPropertyName == "" && grid.Columns[0].ReadOnly &&
                    grid.Columns[0].SortMode == DataGridViewColumnSortMode.NotSortable);
                var keyColumns = grid.Columns.Cast<DataGridViewColumn>()
                    .Where(column => column.DataPropertyName.EndsWith("ID", StringComparison.Ordinal)).ToArray();
                Check(name + " preserves hidden bound key columns", keyColumns.Length > 0 && keyColumns.All(column => !column.Visible));
                var data = MakeData(grid);
                grid.BindingContext = new BindingContext();
                grid.DataSource = data;
                CheckNumbers(name + " initial noncontiguous keys", grid, 3);
                Check(name + " does not rewrite database IDs", ReadKeys(grid, keyColumns[0]).SequenceEqual(new[] { 31, 104, 208 }));
                data.DefaultView.RowFilter = "TestKey <> 104";
                CheckNumbers(name + " after filtering", grid, 2);
                data.DefaultView.Sort = "TestKey DESC";
                CheckNumbers(name + " after sorting", grid, 2);
                Check(name + " sorting retains correct entity identity", ReadKeys(grid, keyColumns[0]).SequenceEqual(new[] { 208, 31 }));
                grid.DataSource = data.Clone();
                CheckNumbers(name + " empty list", grid, 0);
                data.DefaultView.RowFilter = "";
                grid.DataSource = data;
                CheckNumbers(name + " after reload", grid, 3);
            }
            CheckScheduleChronology();
            CheckUnboundRows();
            CheckBindingListUpdates();
            CheckBranchEditorIdentity();
            CheckSubjectEditorIdentity();
            Console.WriteLine($"SUMMARY {checks} checks, 0 failures; no database connections or writes");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            LicenseManager.CurrentContext = oldContext;
        }
    }

    private static void CheckScheduleChronology()
    {
        using var page = new SchedulesForm(); Invoke(page, "ConfigureScheduleGrid");
        var type = typeof(SchedulesForm).Assembly.GetType("University_Timetable_and_Classroom_Management_System.ScheduleRow")!;
        var rows = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
        var entries = new[] { (1, "Tuesday", 10, "Z"), (2, "Wednesday", 8, "A"), (3, "Tuesday", 8, "Z"),
            (4, "Thursday", 8, "A"), (5, "Sunday", 8, "Z"), (6, "Monday", 8, "Z"), (7, "Tuesday", 8, "A") };
        foreach (var (id, day, hour, branch) in entries)
        {
            object row = Activator.CreateInstance(type)!;
            void Set(string key, object value) => type.GetProperty(key)!.SetValue(row, value);
            Set("ScheduleID", id); Set("SemesterNumber", 1); Set("StudyYearID", 3); Set("StudyYearName", "Third Year");
            Set("BranchName", branch); Set("SectionName", "A"); Set("SectionID", id); Set("FacultyMemberID", id);
            Set("SubjectID", id); Set("ClassroomID", id); Set("DayOfWeek", day); Set("TimeSlotID", hour);
            Set("StartTime", TimeSpan.FromHours(hour)); Set("EndTime", TimeSpan.FromHours(hour + 1));
            rows.Add(row);
        }
        typeof(SchedulesForm).GetField("scheduleRows", PrivateInstance)!.SetValue(page, rows);
        Invoke(page, "ApplyScheduleFilters");
        var binding = (BindingSource)typeof(SchedulesForm).GetField("scheduleBindingSource", PrivateInstance)!.GetValue(page)!;
        var ids = binding.Cast<object>().Select(r => (int)type.GetProperty("ScheduleID")!.GetValue(r)!).ToArray();
        Check("Schedule days and times stay chronological across different branches", ids.SequenceEqual(new[] { 5, 6, 7, 3, 1, 2, 4 }));
        Invoke(page, "ApplyScheduleFilters");
        Check("Schedule chronology is retained after filters are reapplied", binding.Cast<object>().Select(r => (int)type.GetProperty("ScheduleID")!.GetValue(r)!).SequenceEqual(ids));
    }

    private static DataTable MakeData(DataGridView grid)
    {
        var data = new DataTable();
        data.Columns.Add("TestKey", typeof(int));
        foreach (var column in grid.Columns.Cast<DataGridViewColumn>().Where(column => column.DataPropertyName != ""))
        {
            data.Columns.Add(column.DataPropertyName, column.DataPropertyName.EndsWith("ID", StringComparison.Ordinal) ? typeof(int) : typeof(string));
        }
        foreach (int id in new[] { 31, 104, 208 })
        {
            var row = data.NewRow();
            row["TestKey"] = id;
            foreach (DataColumn column in data.Columns.Cast<DataColumn>().Skip(1))
                row[column] = column.DataType == typeof(int) ? id : "Row " + id;
            data.Rows.Add(row);
        }
        return data;
    }

    private static void CheckUnboundRows()
    {
        using var grid = NewTestGrid();
        Attach(grid);
        grid.Rows.Add(null, 208);
        grid.Rows.Add(null, 31);
        grid.Rows.Add(null, 104);
        CheckNumbers("Unbound insertion", grid, 3);
        grid.Rows.RemoveAt(1);
        CheckNumbers("Unbound deletion", grid, 2);
        grid.Sort(grid.Columns[1], ListSortDirection.Ascending);
        CheckNumbers("Unbound sorting", grid, 2);
        Check("Unbound sorting retains keys", grid.Rows.Cast<DataGridViewRow>().Select(row => (int)row.Cells[1].Value).SequenceEqual(new[] { 104, 208 }));
        grid.CurrentCell = null;
        grid.Rows[0].Visible = false;
        CheckNumbers("Hidden rows are excluded", grid, 1);
        grid.Rows[0].Visible = true;
        CheckNumbers("Restored hidden row", grid, 2);
        Attach(grid); // Repeated configuration must be harmless.
        grid.Rows.Add(null, 912);
        CheckNumbers("Repeated attachment", grid, 3);
    }

    private static void CheckBindingListUpdates()
    {
        using var grid = NewTestGrid();
        grid.Columns[1].DataPropertyName = nameof(KeyRow.ID);
        grid.BindingContext = new BindingContext();
        Attach(grid);
        var rows = new BindingList<KeyRow> { new(31), new(208) };
        grid.DataSource = rows;
        CheckNumbers("BindingList initial", grid, 2);
        rows.Add(new(104));
        CheckNumbers("BindingList insertion", grid, 3);
        rows.RemoveAt(0);
        CheckNumbers("BindingList deletion", grid, 2);
        Check("BindingList permanent IDs remain unchanged", rows.Select(row => row.ID).SequenceEqual(new[] { 208, 104 }));
    }

    private static void CheckBranchEditorIdentity()
    {
        using var page = new BranchesForm();
        Invoke(page, "ConfigureBranchesGrid");
        var grid = (DataGridView)page.Controls.Find("dgvBranches", true).Single();
        grid.BindingContext = new BindingContext();
        grid.DataSource = new List<Branch> { new() { BranchID = 208, BranchName = "Test branch" } };
        grid.CurrentCell = grid.Rows[0].Cells["colRowNumber"];
        Invoke(page, "PopulateBranchEditorFromSelection");
        Check("Selecting No. 1 populates permanent branch ID 208", grid.Rows[0].Cells["colRowNumber"].Value is 1 &&
            page.Controls.Find("txtBranchId", true).Single().Text == "208");
        object?[] arguments = [0];
        bool valid = (bool)page.GetType().GetMethod("TryGetBranchIdFromEditor", PrivateInstance)!.Invoke(page, arguments)!;
        Check("Branch update/delete target actual key", valid && (int)arguments[0]! == 208);
    }

    private static void CheckSubjectEditorIdentity()
    {
        using var page = new SubjectsForm();
        Invoke(page, "ConfigureSubjectsGrid");
        var grid = (DataGridView)page.Controls.Find("dgvSubjects", true).Single();
        var rowType = page.GetType().GetNestedType("SubjectRow", BindingFlags.NonPublic)!;
        var row = rowType.GetMethod("FromSubject", BindingFlags.Static | BindingFlags.Public)!.Invoke(null,
            [new Subject { SubjectID = 104, SubjectName = "Test subject", StudyYearID = 1, SemesterNumber = 1 }]);
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType))!;
        list.Add(row);
        grid.BindingContext = new BindingContext();
        grid.DataSource = list;
        grid.CurrentCell = grid.Rows[0].Cells["colRowNumber"];
        Invoke(page, "PopulateSubjectEditorFromSelection");
        Check("Selecting No. 1 populates permanent subject ID 104", grid.Rows[0].Cells["colRowNumber"].Value is 1 &&
            page.Controls.Find("txtSubjectId", true).Single().Text == "104");
        object?[] arguments = [0];
        bool valid = (bool)page.GetType().GetMethod("TryGetSubjectIdFromEditor", PrivateInstance)!.Invoke(page, arguments)!;
        Check("Subject update/delete target actual key", valid && (int)arguments[0]! == 104);
    }

    private static DataGridView NewTestGrid()
    {
        var grid = new DataGridView { AllowUserToAddRows = false, AutoGenerateColumns = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRowNumber", ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colKey" });
        return grid;
    }

    private static void Attach(DataGridView grid) => typeof(BranchesForm).Assembly
        .GetType("University_Timetable_and_Classroom_Management_System.GridRowNumbers")!
        .GetMethod("Attach", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [grid, grid.Columns["colRowNumber"]]);

    private static IEnumerable<int> ReadKeys(DataGridView grid, DataGridViewColumn column) =>
        grid.Rows.Cast<DataGridViewRow>().Select(row => Convert.ToInt32(row.Cells[column.Index].Value));

    private static void CheckNumbers(string name, DataGridView grid, int count)
    {
        var rows = grid.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow && row.Visible).ToArray();
        Check(name, rows.Length == count && rows.Select(row => row.Cells["colRowNumber"].Value).SequenceEqual(
            Enumerable.Range(1, count).Cast<object>()));
    }

    private static void Invoke(object instance, string method) => instance.GetType().GetMethod(method, PrivateInstance)!.Invoke(instance, null);
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("FAIL " + name);
        checks++;
        Console.WriteLine("PASS " + name);
    }

    private sealed class DesignContext : LicenseContext
    {
        public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime;
    }
    private sealed record KeyRow(int ID);
}
