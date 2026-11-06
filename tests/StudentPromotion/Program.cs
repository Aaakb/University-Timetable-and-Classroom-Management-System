using Data_Access_Layer;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

var prior = Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
var name = "UniversityTimetable_StudentTest_" + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = name };
Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, connection.ConnectionString);

void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException(message);
    Console.WriteLine("PASS " + message);
}

async Task Reject(Func<Task> action, string message)
{
    try { await action(); }
    catch (ArgumentException) { Check(true, message); return; }
    catch (InvalidOperationException) { Check(true, message); return; }
    throw new Exception(message);
}

try
{
    await using (var db = new AppDbContext())
    {
        Check(db.Database.GetDbConnection().Database == name, "Database isolated");
        await db.Database.EnsureCreatedAsync();
        // Simulate an existing installation created before the Students table.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS dbo.Students");
        db.StudyYears.AddRange(Enumerable.Range(1, 4).Select(n => new StudyYear { StudyYearID = n, YearName = $"Year {n}" }));
        db.Branches.AddRange(new Branch { BranchID = 1, BranchName = "Computer Science" }, new Branch { BranchID = 2, BranchName = "Cyber Security" });
        db.Sections.AddRange(
            new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 35 },
            new Section { SectionID = 2, SectionName = "B", StudyYearID = 1 },
            new Section { SectionID = 3, SectionName = "A", StudyYearID = 2 },
            new Section { SectionID = 4, SectionName = "B", StudyYearID = 2 },
            new Section { SectionID = 5, SectionName = "A", StudyYearID = 3, BranchID = 1 },
            new Section { SectionID = 6, SectionName = "A", StudyYearID = 3, BranchID = 2 },
            new Section { SectionID = 7, SectionName = "A", StudyYearID = 4, BranchID = 1 });
        await db.SaveChangesAsync();
    }
    await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
    await new DatabaseMaintenanceService().ApplyPendingFixesAsync();
    var service = new StudentService();
    var one = await service.AddAsync(" First Student ", 1, 1);
    var two = await service.AddAsync("Second Student", 1, 2);
    var three = await service.AddAsync("Third Student", 2);
    Check((await service.ListAsync(1)).Select(s => s.FullName).SequenceEqual(new[] { "First Student", "Second Student" }), "Names are saved in the selected stage");
    await Reject(() => service.PromoteAsync(1, [one.StudentID, three.StudentID]), "A mixed-stage promotion is rejected");
    Check((await service.ListAsync(1)).Count == 2, "A rejected promotion changes no rows");
    await service.PromoteAsync(1, [one.StudentID, two.StudentID]);
    Check((await service.ListAsync(1)).Count == 0 && (await service.ListAsync(2)).Count == 3, "A batch moves one stage only");
    Check((await service.ListAsync(2)).Single(s => s.StudentID == two.StudentID).SectionID == 4, "Promotion retains section B");
    await service.AssignGroupAsync(2, [three.StudentID], 3);
    Check((await service.ListAsync(2)).Single(s => s.StudentID == three.StudentID).SectionID == 3, "Previously unassigned students can be distributed");
    await Reject(() => service.PromoteAsync(1, [one.StudentID]), "A stale promotion cannot advance the same student again");
    await Reject(() => service.PromoteAsync(2, [one.StudentID]), "Promotion to stage three requires a specialization");
    await Reject(() => service.PromoteAsync(2, [one.StudentID], 7), "A specialization from the wrong stage is rejected");
    await service.PromoteAsync(2, [one.StudentID], 5);
    await service.PromoteAsync(3, [one.StudentID]);
    await Reject(() => service.PromoteAsync(4, [one.StudentID]), "Stage four cannot advance beyond the final stage");
    Check((await service.ListAsync(4)).Single() is { SectionID: 7 } graduate && graduate.StudentID == one.StudentID,
        "The student keeps the same identity and specialization through promotions");
    await using var verification = new AppDbContext();
    Check(await verification.Sections.Where(s => s.SectionID == 1).Select(s => s.StudentCount).SingleAsync() == 35, "Promotion preserves scheduling counts");
    string csv = Path.Combine(Path.GetTempPath(), name + ".csv");
    string xlsx = Path.ChangeExtension(csv, ".xlsx");
    try
    {
        await File.WriteAllTextAsync(csv, "اسم الطالب\r\nأحمد علي\r\n\"طالب, ثانٍ\"\r\nأحمد علي\r\n");
        Check(await service.ImportAsync(csv, 3, 6) == (2, 1), "UTF-8 CSV imports quoted names and skips duplicates");
        Check(await service.ImportAsync(csv, 3, 6) == (0, 3) && (await service.ListAsync(3)).All(s => s.SectionID == 6),
            "Repeated CSV import is idempotent within the selected specialization");
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("الطلبة");
            sheet.Cell(1, 1).Value = "اسم الطالب";
            sheet.Cell(2, 1).Value = "أحمد علي";
            workbook.SaveAs(xlsx);
        }
        Check(await service.ImportAsync(xlsx, 2) == (1, 0) && (await service.ListAsync(3)).Count == 2,
            "Excel imports into the selected stage without changing other stages");
        await File.WriteAllTextAsync(csv, "اسم الطالب\r\nاسم صالح\r\nعمود,زائد\r\n");
        await Reject(() => service.ImportAsync(csv, 3), "Malformed CSV is rejected");
        Check((await service.ListAsync(3)).Count == 2, "Invalid input inserts no partial list");
    }
    finally
    {
        File.Delete(csv);
        File.Delete(xlsx);
    }
    string rosterXlsx = Path.Combine(Path.GetTempPath(), name + "_roster.xlsx");
    string rosterPdf = Path.ChangeExtension(rosterXlsx, ".pdf");
    try
    {
        RosterExportService.Export(rosterXlsx, "قائمة التدريسيين", "اسم التدريسي", "اللقب العلمي",
            [new("أحمد علي", "أ.م.د"), new("ليلى حسن", "")]);
        using (var book = new XLWorkbook(rosterXlsx))
        {
            var sheet = book.Worksheet(1);
            Check(sheet.RightToLeft && sheet.Cell("B2").GetString() == "اسم التدريسي" &&
                sheet.Cell("B3").GetString() == "أحمد علي" && sheet.Cell("C3").GetString() == "أ.م.د" &&
                sheet.Cell("A4").GetValue<int>() == 2 && sheet.Cell("C4").IsEmpty() && sheet.LastColumnUsed()!.ColumnNumber() == 3,
                "Excel preserves Arabic names, optional titles and row numbers without identifiers");
        }
        var roster = Enumerable.Range(1, 50).Select(i => new RosterRow("ليلى حسن عبد الله", i <= 25 ? "B1" : "B2")).ToArray();
        RosterExportService.Export(rosterPdf, "طلبة المرحلة الثانية - شعبة B", "اسم الطالب", "الشعبة أو التخصص", roster);
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(rosterPdf))
        {
            Check(pdf.NumberOfPages == 2 && pdf.GetPage(1).Text.Contains("B1") && pdf.GetPage(2).Text.Contains("B2"),
                "PDF paginates a long student roster and prints the subgroup codes");
        }
        if (Environment.GetEnvironmentVariable("ROSTER_PDF_PREVIEW") is { } previewPath)
            File.Copy(rosterPdf, previewPath, overwrite: true);
    }
    finally
    {
        File.Delete(rosterXlsx);
        File.Delete(rosterPdf);
    }
    await service.AddAsync("Stays in first year", 1, 1);
    await service.AddAsync("Ready for second year", 1, 2);
    var deletedStudent = await service.AddAsync("طالب للاختبار", 1, 1);
    await Reject(() => service.DeleteAsync(deletedStudent.StudentID, 1, 2), "A stale section cannot delete a student from another section");
    await service.DeleteAsync(deletedStudent.StudentID, 1, 1);
    Check((await service.ListAsync(1)).Count == 2 && (await service.ListAsync(1)).All(s => s.StudentID != deletedStudent.StudentID),
        "Deleting a student removes only the specified record");
    await new FacultyMemberService().AddAsync(new FacultyMember { FullName = "تدريسي للاختبار" });
    var uiResult = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var uiThread = new Thread(() =>
    {
        try
        {
            using var form = new Form { Width = 1050, Height = 720, ShowInTaskbar = false };
            using var page = new StudyYearsForm { Dock = DockStyle.Fill };
            form.Controls.Add(page);
            form.Shown += async (_, _) =>
            {
                try
                {
                    var host = page.Controls.Find("academicPageHost", true).Single();
                    Control VisibleNavigation() => page.Controls.Find("academicPageNavigation", true).Single(bar => bar.Visible);
                    void Navigate(string destination) => ((IButtonControl)VisibleNavigation().Controls[$"btnAcademic{destination}"]!).PerformClick();
                    void CheckNavigation(Control editor, Control table)
                    {
                        var bar = VisibleNavigation();
                        Check(bar.Controls.Cast<Control>().Select(b => b.Text).SequenceEqual(new[] { "المراحل", "الشعب", "الطلبة" }) &&
                            bar.Controls.Cast<Control>().All(b => b.Visible && b.Font.SizeInPoints >= 13),
                            "Academic navigation displays large stage, section and student buttons in order");
                        Check(editor.PointToScreen(new Point(0, editor.Height)).Y <= bar.PointToScreen(Point.Empty).Y &&
                            bar.PointToScreen(new Point(0, bar.Height)).Y <= table.PointToScreen(Point.Empty).Y &&
                            Math.Abs(bar.Left - (bar.Parent!.ClientSize.Width - bar.Width) / 2) <= 4,
                            "Academic navigation is centered between editor actions and the table");
                    }
                    var grid = (DataGridView)page.Controls.Find("dgvStudyYears", true).Single();
                    CheckNavigation(page.Controls.Find("pnlStudyYearEditor", true).Single(), grid);
                    Check(!host.Controls.OfType<SectionsForm>().Any(), "The sections page is loaded when opened");
                    Navigate("Sections");
                    var sections = host.Controls.OfType<SectionsForm>().Single();
                    var sectionGrid = (DataGridView)sections.Controls.Find("dgvSections", true).Single();
                    for (int i = 0; i < 100 && sectionGrid.Rows.Count < 7; i++) await Task.Delay(50);
                    Check(sectionGrid.Visible && sectionGrid.Rows.Count == 7 &&
                        sections.Controls.Find("btnAddSection", true).Single().Visible,
                        "The entry opens the complete existing section editor and list");
                    CheckNavigation(sections.Controls.Find("pnlSectionEditor", true).Single(), sectionGrid);
                    Navigate("Stages");
                    var list = (DataGridView)page.Controls.Find("dgvStudents", true).Single();
                    void ClickCell(DataGridView table, string column, int row)
                    {
                        table.CurrentCell = table.Rows[row].Cells[column];
                        typeof(DataGridView).GetMethod("OnCellClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                            .Invoke(table, [new DataGridViewCellEventArgs(table.Columns[column]!.Index, row)]);
                    }
                    async Task SelectStage(int stage)
                    {
                        if (!grid.Visible) Navigate("Stages");
                        for (int i = 0; i < 100 && grid.Rows.Count < 4; i++) await Task.Delay(50);
                        int row = grid.Rows.Cast<DataGridViewRow>().Single(r => ((StudyYear)r.DataBoundItem!).StudyYearID == stage).Index;
                        ClickCell(grid, "colYearName", row);
                    }
                    await SelectStage(1);
                    for (int i = 0; i < 100 && list.Rows.Count != 1; i++) await Task.Delay(50);
                    Check(list.Visible && list.Rows.Count == 1 && list.Rows[0].Cells["colStudentPromotion"].Value is true &&
                        Equals(list.Rows[0].Cells["colStudentName"].Value, "Stays in first year"),
                        "Clicking the first stage opens its section A directly");
                    CheckNavigation(page.Controls.Find("pnlStudentEditor", true).Single(), list);
                    var stageFilter = (ComboBox)page.Controls.Find("cmbStudentStage", true).Single();
                    var groupFilter = (ComboBox)page.Controls.Find("cmbStudentGroup", true).Single();
                    var filterBar = stageFilter.Parent!.Parent!;
                    Check(stageFilter.Visible && stageFilter.SelectedIndex == 0 && stageFilter.Items.Count == 4 &&
                        filterBar == groupFilter.Parent!.Parent && filterBar.Name == "pnlStudentListFilters" &&
                        stageFilter.PointToScreen(Point.Empty).X > groupFilter.PointToScreen(Point.Empty).X &&
                        page.Controls.Find("btnBackToStages", true).Length == 0 &&
                        list.RowTemplate.Height == sectionGrid.RowTemplate.Height &&
                        list.ColumnHeadersDefaultCellStyle.BackColor == sectionGrid.ColumnHeadersDefaultCellStyle.BackColor,
                        "The stage filter comes first in the right-to-left list header");
                    Check(groupFilter.Items.Cast<object>().Select(item => item.ToString()).SequenceEqual(new[] { "شعبة A", "شعبة B", "غير موزّعين" }) &&
                        groupFilter.SelectedIndex == 0 &&
                        ((Label)page.Controls.Find("lblStudentSummary", true).Single()).TextAlign == ContentAlignment.MiddleCenter,
                        "Only A, B and unassigned are available, with the student summary centered");
                    list.Rows[0].Cells["colStudentPromotion"].Value = false;
                    Check(list.Rows[0].Cells["colStudentPromotion"].Value is false, "A student can be excluded individually");
                    var editName = page.Controls.Find("btnEditStudentName", true).SingleOrDefault();
                    var deleteStudent = page.Controls.Find("btnDeleteStudent", true).Single();
                    var exportStudents = page.Controls.Find("btnExportStudents", true).Single();
                    Check(editName is { Visible: true, Enabled: false, Text: "تعديل" } &&
                        deleteStudent is { Visible: true, Enabled: false, Text: "حذف" } && deleteStudent.Parent == editName.Parent,
                        "Edit and delete are visible beside each other and require a selected row");
                    Check(exportStudents.PointToScreen(Point.Empty).X > editName!.PointToScreen(Point.Empty).X &&
                        editName.PointToScreen(Point.Empty).X > deleteStudent.PointToScreen(Point.Empty).X,
                        "Export appears before edit and delete in the right-to-left toolbar");
                    list.CurrentCell = list.Rows[0].Cells["colStudentName"];
                    list.Rows[0].Selected = true;
                    Check(editName!.Enabled && deleteStudent.Enabled, "Selecting a student enables edit and delete");
                    int studentId = (int)list.Rows[0].Tag!;
                    Exception? dialogError = null;
                    using var editDialogTimer = new System.Windows.Forms.Timer { Interval = 30 };
                    editDialogTimer.Tick += (_, _) =>
                    {
                        var dialog = Application.OpenForms["dlgEditStudentName"];
                        if (dialog is null) return;
                        editDialogTimer.Stop();
                        try
                        {
                            var input = dialog.Controls.Find("txtEditedStudentName", true).Single();
                            Check(input.Text == "Stays in first year", "The editor shows the selected student's existing name");
                            input.Text = " ";
                            Check(!((Control)dialog.AcceptButton!).Enabled, "The editor disables saving an empty name");
                            input.Text = "  اسم الطالب المصحح  ";
                            dialog.AcceptButton!.PerformClick();
                        }
                        catch (Exception ex) { dialogError = ex; dialog.Close(); }
                    };
                    editDialogTimer.Start();
                    ((IButtonControl)editName).PerformClick();
                    editDialogTimer.Stop();
                    if (dialogError is not null) throw dialogError;
                    for (int i = 0; i < 100 && (list.Rows.Count == 0 || !Equals(list.Rows[0].Cells["colStudentName"].Value, "اسم الطالب المصحح")); i++)
                        await Task.Delay(50);
                    var renamed = (await service.ListAsync(1)).Single(s => s.StudentID == studentId);
                    Check(renamed.FullName == "اسم الطالب المصحح" && renamed.SectionID == 1 && list.Rows.Count == 1 &&
                        Equals(list.Rows[0].Cells["colStudentName"].Value, renamed.FullName) &&
                        list.Rows[0].Cells["colStudentPromotion"].Value is false, "Saving a correction changes only the name and keeps the promotion checkbox");
                    groupFilter.SelectedIndex = 1;
                    for (int i = 0; i < 100 && (list.Rows.Count == 0 || !Equals(list.Rows[0].Cells["colStudentName"].Value, "Ready for second year")); i++)
                        await Task.Delay(50);
                    Check(list.Rows.Count == 1 && Equals(list.Rows[0].Cells["colStudentName"].Value, "Ready for second year"),
                        "The header filter switches directly to the students of section B");
                    if (Environment.GetEnvironmentVariable("STUDENT_UI_PREVIEW") is { } previewPath)
                    {
                        using var preview = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                        preview.Save(previewPath);
                    }
                    stageFilter.SelectedIndex = 1;
                    var target = (ComboBox)page.Controls.Find("cmbStudentDestination", true).Single();
                    var promoteButton = page.Controls.Find("btnPromoteStudents", true).Single();
                    for (int i = 0; i < 100 && (!target.Enabled || list.Rows.Count == 0); i++) await Task.Delay(50);
                    Check(list.Visible && groupFilter.SelectedItem?.ToString() == "شعبة A" && list.Rows.Count == 1 &&
                        Equals(list.Rows[0].Cells["colStudentName"].Value, "Third Student"),
                        "Switching to year two stays on the student list and defaults to section A");
                    Check(target.Visible && promoteButton.Visible && target.Parent == promoteButton.Parent &&
                        target.SelectedIndex == 0 && !promoteButton.Enabled &&
                        page.Controls.Find("lblStudentPromotionDestination", true).Single().Text.Contains("بعد الترقية"),
                        "The destination major is grouped with promotion and must be selected first");
                    target.SelectedIndex = 1;
                    Check(promoteButton.Enabled && promoteButton.Text.Contains("الثالثة"), "Selecting a major enables promotion to year three");
                    Navigate("Sections");
                    int sectionB = sectionGrid.Rows.Cast<DataGridViewRow>().Single(row =>
                        Equals(row.DataBoundItem?.GetType().GetProperty("SectionID")?.GetValue(row.DataBoundItem), 2)).Index;
                    ClickCell(sectionGrid, "colSectionName", sectionB);
                    for (int i = 0; i < 100 && (list.Rows.Count == 0 || !Equals(list.Rows[0].Cells["colStudentName"].Value, "Ready for second year")); i++) await Task.Delay(50);
                    Check(list.Rows.Count == 1 && Equals(list.Rows[0].Cells["colStudentName"].Value, "Ready for second year") &&
                        groupFilter.SelectedItem?.ToString() == "شعبة B", "Clicking section B opens only its own students");
                    var splitSection = page.Controls.Find("btnSplitStudentSection", true).Single();
                    Check(splitSection.Visible && !splitSection.Enabled && list.Rows[0].Cells["colStudentSection"] is DataGridViewLinkCell,
                        "The section can be changed from the student row and a one-student section cannot be split");
                    var moved = await service.AddAsync("حسن", 1, 1);
                    await service.AddAsync("زينب", 1, 1);
                    await service.SplitSectionAsync(1, 1);
                    await service.ChangeSectionAsync(1, moved.StudentID, 1, 2);
                    await Reject(() => service.ChangeSectionAsync(1, moved.StudentID, 1, 2), "A stale section change cannot move the student again");
                    var afterMove = (await service.ListAsync(1)).Single(s => s.StudentID == moved.StudentID);
                    Check(afterMove.SectionID == 2 && afterMove.PracticalGroupNumber is null && afterMove.FullName == "حسن",
                        "Changing section moves the same student and clears the former subgroup");
                    foreach (string studentName in new[] { "ياسر", "أحمد", "باسم" }) await service.AddAsync(studentName, 1, 2);
                    await service.SplitSectionAsync(1, 1);
                    await service.SplitSectionAsync(1, 2);
                    var splits = await service.ListAsync(1);
                    Check(splits.Count(s => s.SectionID == 1 && s.PracticalGroupNumber == 1) == 1 &&
                        splits.Count(s => s.SectionID == 1 && s.PracticalGroupNumber == 2) == 1 &&
                        splits.Count(s => s.SectionID == 2 && s.PracticalGroupNumber == 1) == 3 &&
                        splits.Count(s => s.SectionID == 2 && s.PracticalGroupNumber == 2) == 2,
                        "Even and odd sections split independently with a difference of at most one");
                    Navigate("Sections");
                    ClickCell(sectionGrid, "colSectionName", sectionB);
                    for (int i = 0; i < 100 && (list.Rows.Count != 5 || !splitSection.Enabled); i++) await Task.Delay(50);
                    var displayed = list.Rows.Cast<DataGridViewRow>().ToArray();
                    var alphabet = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ar-IQ"), ignoreCase: true);
                    var ordered = splits.Where(s => s.SectionID == 2).OrderBy(s => s.PracticalGroupNumber).ThenBy(s => s.FullName, alphabet);
                    Check(splitSection.Visible && splitSection.Enabled && splitSection.Text.Contains("B") &&
                        page.Controls.Find("btnExportStudents", true).Single().Enabled &&
                        displayed.Select(row => (row.Cells["colStudentSection"].Value, row.Cells["colStudentName"].Value))
                            .SequenceEqual(ordered.Select(s => ((object)$"B{s.PracticalGroupNumber}", (object)s.FullName))),
                        "The two subgroups display their labels and students in Arabic alphabetical order");
                    await service.PromoteAsync(1, [moved.StudentID]);
                    Check((await service.ListAsync(2)).Any(s => s.StudentID == moved.StudentID && s.SectionID == 4 && s.PracticalGroupNumber is null),
                        "Promotion preserves the main section and clears the old stage subgroup");
                    Navigate("Sections");
                    int specialization = sectionGrid.Rows.Cast<DataGridViewRow>().Single(row =>
                        Equals(row.DataBoundItem?.GetType().GetProperty("SectionID")?.GetValue(row.DataBoundItem), 7)).Index;
                    ClickCell(sectionGrid, "colBranch", specialization);
                    for (int i = 0; i < 100 && (list.Rows.Count == 0 || !Equals(list.Rows[0].Cells["colStudentName"].Value, "First Student")); i++) await Task.Delay(50);
                    Check(stageFilter.SelectedIndex == 3 && list.Rows.Count == 1 && Equals(list.Rows[0].Cells["colStudentName"].Value, "First Student"),
                        "Clicking a specialization opens only its students");
                    using var facultyPage = new FacultyMembersForm { Dock = DockStyle.Fill };
                    page.Hide();
                    form.Controls.Add(facultyPage);
                    var facultyExport = facultyPage.Controls.Find("btnExportFaculty", true).Single();
                    for (int i = 0; i < 100 && !facultyExport.Enabled; i++) await Task.Delay(50);
                    Check(facultyExport.Visible && facultyExport.Enabled, "The faculty roster offers export after its rows load");
                    using var shell = new MainShellForm { ShowInTaskbar = false };
                    var sidebar = (Guna.UI2.WinForms.Guna2Panel)shell.Controls.Find("pnlSidebar", true).Single();
                    var active = (Guna.UI2.WinForms.Guna2Button)shell.Controls.Find("btnNavigationBranches", true).Single();
                    var inactive = (Guna.UI2.WinForms.Guna2Button)shell.Controls.Find("btnNavigationSubjects", true).Single();
                    Check(sidebar.FillColor == Color.FromArgb(52, 78, 92) && active.Checked && !active.Enabled &&
                        active.DisabledState.FillColor == Color.FromArgb(74, 100, 116) && active.DisabledState.ForeColor == Color.White &&
                        active.DisabledState.BorderColor == Color.FromArgb(185, 200, 205) && !inactive.Checked && inactive.Enabled,
                        "The current sidebar page has a distinct fill, border and white text in the mist-blue theme");
                    uiResult.TrySetResult();
                }
                catch (Exception ex) { uiResult.TrySetException(ex); }
                finally { form.Close(); }
            };
            Application.Run(form);
        }
        catch (Exception ex) { uiResult.TrySetException(ex); }
    }) { IsBackground = true };
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start();
    await uiResult.Task.WaitAsync(TimeSpan.FromSeconds(15));
}
finally
{
    await using var db = new AppDbContext();
    if (db.Database.GetDbConnection().Database != name || !name.StartsWith("UniversityTimetable_StudentTest_", StringComparison.Ordinal))
        throw new Exception("Unsafe cleanup");
    await db.Database.EnsureDeletedAsync();
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, prior);
}
