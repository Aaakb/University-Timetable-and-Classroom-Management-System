using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class DesignerChecks
{
    static readonly List<object> results = [];
    static string output = "";
    static Assembly assembly = null!;
    static int failures;
    [STAThread] static void Main(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("Pass application DLL, output directory, and isolated test database.");
        string application = Path.GetFullPath(args[0]);
        output = Path.GetFullPath(args[1]);
        string database = args[2];
        if (!database.StartsWith("UniversityTimetable_FourFixes_", StringComparison.Ordinal)) throw new ArgumentException("Only the isolated regression database is allowed.");
        string server = Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_TEST_SERVER") ?? "localhost";
        Environment.SetEnvironmentVariable("UNIVERSITY_TIMETABLE_CONNECTION_STRING", $"Server={server};Database={database};Integrated Security=True;Encrypt=False;TrustServerCertificate=True");
        string directory = Path.GetDirectoryName(application)!;
        var resolver=new AssemblyDependencyResolver(application);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string? path=resolver.ResolveAssemblyToPath(name);
            if(path is null && File.Exists(Path.Combine(directory,name.Name+".dll")))path=Path.Combine(directory,name.Name+".dll");
            return path is null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        };
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) => resolver.ResolveUnmanagedDllToPath(name) is { } path ? NativeLibrary.Load(path) : IntPtr.Zero;
        Console.WriteLine("Loading application assembly: " + application);
        assembly = Assembly.LoadFrom(application);
        EnsureSelectionFixture().GetAwaiter().GetResult();
        if(args.Contains("diagnostics")) AppDomain.CurrentDomain.FirstChanceException += (_,e) => Console.WriteLine("DIAGNOSTIC " + e.Exception);
        Directory.CreateDirectory(output);
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
        {
            failures++;
            results.Add(new { name = "Unhandled UI exception", passed = false, error = e.Exception.ToString() });
            Console.WriteLine("FAIL Unhandled UI exception: " + e.Exception);
        };
        Console.WriteLine("Creating the shell");
        using var shell = (Form)Create("MainShellForm");
        shell.Text = "Designer compatibility regression";
        Console.WriteLine("Shell constructed");
        shell.Shown += async (_, _) =>
        {
            try
            {
                if (args.Contains("review"))
                {
                    shell.Size = new Size(1280, 960);
                    var teachingButton = shell.Controls.Find("btnNavigationFacultyAssignments", true).Single();
                    teachingButton.GetType().GetMethod("PerformClick")!.Invoke(teachingButton, null);
                    Console.WriteLine("UI_READY_FOR_VISUAL_REVIEW - close the window when finished");
                    return;
                }
                if (args.Contains("displays"))
                    await CheckDisplays(shell,args.Contains("small"));
                else {
                foreach (int width in new[] {1280,1600})
                {
                    shell.Size = new Size(width, 960);
                    foreach (string page in new[] {"Branches", "StudyYears", "Sections", "Subjects", "Classrooms", "TimeSlots", "FacultyMembers", "FacultyAssignments", "Schedules"})
                    {
                        var navigation = shell.Controls.Find("btnNavigation" + page, true).Single();
                        var transition = System.Diagnostics.Stopwatch.StartNew();
                        if (navigation.Enabled) navigation.GetType().GetMethod("PerformClick")!.Invoke(navigation, null);
                        Console.WriteLine($"NAVIGATION {page} {width}px synchronous transition: {transition.ElapsedMilliseconds}ms");
                        await Task.Delay(600);
                        var host = shell.Controls.Find("pnlPageHost", true).Single();
                        var content = host.Controls[0];
                        string expected = page == "FacultyAssignments" ? "FacultyMemberSubjectsForm" : page + "Form";
                        Check($"Navigation opens {page} at {width}", content.GetType().Name == expected && !navigation.Enabled);
                        var pageViewport=(ScrollableControl)host;
                        Check($"{page} host avoids unnecessary scrollbars at {width}",
                            content.MinimumSize.Width > host.ClientSize.Width || content.MinimumSize.Height > host.ClientSize.Height ||
                            (!pageViewport.HorizontalScroll.Visible && !pageViewport.VerticalScroll.Visible));
                        Save(content, $"{page}-{width}");
                        var buttons = Children(content).Where(c => c.Visible && c.Name.StartsWith("btn") && !c.Name.StartsWith("btnNavigation")).ToList();
                        Check($"{page} buttons reachable through the page scrollbars at {width}", buttons.All(c => CanReach(c, shell)));
                        foreach (var scroll in Children(shell).OfType<ScrollableControl>().Where(c => c.AutoScroll)) scroll.AutoScrollPosition = Point.Empty;
                        Check($"{page} action buttons retain their handlers at {width}", buttons.All(HasClickHandler));
                        var grids = Children(content).OfType<DataGridView>().Where(g => g.Name != "dgvTimetable").ToArray();
                        Check($"{page} keeps explicit data-bound designer columns at {width}", grids.Length == 1 && grids.All(g => !g.AutoGenerateColumns && g.DataSource != null && g.Columns.Cast<DataGridViewColumn>().All(c => c.Name == "colRowNumber" || !string.IsNullOrWhiteSpace(c.DataPropertyName))));
                        if (page == "TimeSlots") CheckTimeSlotDefaults(content);
                        if (grids.Length == 1)
                        {
                            var recordGrid = grids[0];
                            await CheckRecordEditorSelection(content, recordGrid, page, width);
                            var numberColumn = recordGrid.Columns["colRowNumber"];
                            Check($"{page} displays contiguous row numbers at {width}", numberColumn is not null && numberColumn.Visible &&
                                string.IsNullOrEmpty(numberColumn.DataPropertyName) && recordGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Visible && !r.IsNewRow)
                                    .Select((r, i) => Equals(r.Cells[numberColumn.Index].Value, i + 1)).All(value => value));
                            var clippedHeaders = recordGrid.Columns.Cast<DataGridViewColumn>()
                                .Where(c => c.Visible && !c.HeaderText.Any(char.IsWhiteSpace) && c.Width < c.GetPreferredWidth(DataGridViewAutoSizeColumnMode.ColumnHeader, true))
                                .Select(c => $"{c.Name}: {c.Width}px, needs {c.GetPreferredWidth(DataGridViewAutoSizeColumnMode.ColumnHeader, true)}px").ToArray();
                            if (clippedHeaders.Length > 0) Console.WriteLine("CLIPPED_HEADERS " + string.Join("; ", clippedHeaders));
                            Check($"{page} single-word column headers fit at {width}", clippedHeaders.Length == 0);
                        }
                        if (page == "Schedules")
                        {
                            if (width == 1280) await CheckPendingEditorClear(content);
                            Check("All schedule editor controls exist", new[]{"cmbLectureType","cmbGroupName","cmbSemesterFilter","cmbGroupFilter","btnScheduleGridView","btnScheduleTimetableView"}.All(name=>content.Controls.Find(name,true).Length==1));
                            var records = (DataGridView)content.Controls.Find("dgvSchedules",true).Single();
                            Check("The record grid retains 13 bound columns including the lock and the row number column", records.Columns.Count == 14 && records.Columns.Cast<DataGridViewColumn>().Count(c => !string.IsNullOrEmpty(c.DataPropertyName)) == 13 && records.Columns["colLockStatus"].DataPropertyName == "IsLocked");
                            content.GetType().GetMethod("ShowScheduleTimetableView",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(content,null);
                            await Task.Delay(100);
                            Save(content,$"Schedules-weekly-{width}");
                            if (content.Controls.Find("dgvTimetable",true).FirstOrDefault() is DataGridView weekly)
                            {
                                Check("The weekly grid has six designer columns", weekly.Columns.Count==6);
                                Check("Weekly view contains data rows", weekly.Rows.Count>0);
                                Check("Weekly view does not construct controls for data records", weekly.Controls.Count<=2);
                                CheckWeeklySelectionAndScrolling(content,weekly);
                            }
                        }
                    }
                }
                Save(shell,"MainShellForm");
                var selectedSchedulePage = shell.Controls.Find("pnlPageHost", true).Single().Controls[0];
                shell.Controls.Find("btnNavigationBranches", true).Single().GetType().GetMethod("PerformClick")!.Invoke(shell.Controls.Find("btnNavigationBranches", true).Single(), null);
                await Task.Delay(300);
                Check("Leaving a selected schedule disposes its page safely", selectedSchedulePage.IsDisposed);
                foreach(string name in new[]{"LoginForm","RegisterForm"})
                {
                    using var form=(Form)Create(name);
                    form.Show();await Task.Delay(150);Save(form,name);
                    var buttons = Children(form).Where(c => c.Name.StartsWith("btn")).ToArray();
                    Check(name + " buttons retain their handlers", buttons.Length > 0 && buttons.All(HasClickHandler));
                    Check(name + " keeps its default keyboard button", form.AcceptButton is Control accept && accept.Name == (name == "LoginForm" ? "btnSignIn" : "btnCreateAccount"));
                    if (name == "RegisterForm") Check("RegisterForm keeps its Cancel button", form.CancelButton is Control cancel && cancel.Name == "btnCancel");
                    CheckPasswordToggle(form, "btnTogglePassword", "txtPassword");
                    if (name == "RegisterForm") CheckPasswordToggle(form, "btnToggleConfirmPassword", "txtConfirmPassword");
                    form.Close();
                }
                }
            }
            catch(Exception ex) { failures++; results.Add(new{name="UI harness",passed=false,error=ex.ToString()}); Console.WriteLine(ex); }
            File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"SUMMARY {results.Count} checks, {failures} failures");
            shell.Close();
        };
        Console.WriteLine("Starting UI message loop");
        Application.Run(shell);
        Environment.ExitCode=failures==0?0:1;
    }
    static Control Create(string name)=>(Control)Activator.CreateInstance(assembly.GetType("University_Timetable_and_Classroom_Management_System."+name)!)!;
    static async Task CheckDisplays(Form shell,bool small)
    {
        Console.WriteLine($"DISPLAY DeviceDpi={shell.DeviceDpi}; working area={Screen.FromControl(shell).WorkingArea}");
        Console.WriteLine("PHYSICAL_MONITORS "+string.Join("; ",Screen.AllScreens.Select(s=>$"{s.DeviceName} {s.Bounds} {s.WorkingArea}")));
        var resolutions=small?new[]{new Size(1280,720)}:new[]{new Size(1280,720),new Size(1366,768),new Size(1600,900),new Size(1920,1080),new Size(2560,1440)};
        foreach(var resolution in resolutions)
        foreach(double zoom in small?new[]{2.0}:new[]{1.0,1.25,1.5,2.0})
        {
            // Equivalent available space at this zoom; do not change the user's Windows display settings.
            var available=new Size((int)(resolution.Width/zoom),(int)((resolution.Height-40)/zoom));
            string scenario=$"{resolution.Width}x{resolution.Height}-{zoom*100:0}%";
            shell.WindowState=FormWindowState.Normal;
            shell.Size=available;
            Check($"Shell fits {scenario}",shell.Width<=available.Width&&shell.Height<=available.Height);
            foreach(string page in new[]{"Branches","StudyYears","Sections","Subjects","Classrooms","TimeSlots","FacultyMembers","FacultyAssignments","Schedules"})
            {
                var navigation=shell.Controls.Find("btnNavigation"+page,true).Single();
                if(navigation.Enabled)navigation.GetType().GetMethod("PerformClick")!.Invoke(navigation,null);
                await Task.Delay(100);
                var content=shell.Controls.Find("pnlPageHost",true).Single().Controls[0];
                var unreachable=Children(content).Where(IsEssential).Where(c=>!CanReach(c,shell)).Select(c=>c.Name).ToArray();
                if(unreachable.Length>0)Console.WriteLine($"UNREACHABLE {page}/{scenario}: {string.Join(",",unreachable)}");
                Check($"{page} controls reachable at {scenario}",unreachable.Length==0);
                CheckNoOverlaps(content,$"{page}/{scenario}");
                Check($"{page} sidebar action reachable at {scenario}",CanReach(navigation,shell));
                foreach(var scroll in Children(shell).OfType<ScrollableControl>())if(scroll.AutoScroll)scroll.AutoScrollPosition=Point.Empty;
                if((resolution.Width==1366&&zoom==1)||(resolution.Width==1280&&zoom==2))
                {
                    Save(content,$"Display-{page}-{scenario}");
                    Save(shell,$"Display-Shell-{page}-{scenario}");
                }
            }
            foreach(string name in new[]{"LoginForm","RegisterForm"})
            {
                using var form=(Form)Create(name);
                form.Show();
                form.Size=new Size(Math.Min(form.Width,available.Width),Math.Min(form.Height,available.Height));
                await Task.Delay(30);
                Check($"{name} fits {scenario}",form.Width<=available.Width&&form.Height<=available.Height);
                var unreachable=Children(form).Where(IsEssential).Where(c=>!CanReach(c,form)).Select(c=>c.Name).ToArray();
                if(unreachable.Length>0)Console.WriteLine($"UNREACHABLE {name}/{scenario}: {string.Join(",",unreachable)}");
                Check($"{name} controls reachable at {scenario}",unreachable.Length==0);
                CheckNoOverlaps(form,$"{name}/{scenario}");
                foreach(var scroll in Children(form).OfType<ScrollableControl>())if(scroll.AutoScroll)scroll.AutoScrollPosition=Point.Empty;
                if(resolution.Width==1280&&zoom==2)Save(form,$"Display-{name}-{scenario}");
                form.Close();
            }
        }
    }
    static bool IsEssential(Control control)=>control.Visible &&
        (control.Name.StartsWith("btn")||control.Name.StartsWith("txt")||control.Name.StartsWith("cmb")||control.Name.StartsWith("dtp")||control is DataGridView);
    static void CheckNoOverlaps(Control page,string name)
    {
        var overlaps=new List<string>();
        foreach(var group in Children(page).Where(IsEssential).Where(c=>!c.Name.StartsWith("btnToggle")).GroupBy(c=>c.Parent))
        {
            var controls=group.ToArray();
            for(int i=0;i<controls.Length;i++)for(int j=i+1;j<controls.Length;j++)
                if(controls[i].Bounds.IntersectsWith(controls[j].Bounds))overlaps.Add(controls[i].Name+"/"+controls[j].Name);
        }
        if(overlaps.Count>0)Console.WriteLine("OVERLAP "+name+": "+string.Join(",",overlaps));
        Check(name+" fields and action buttons do not overlap",overlaps.Count==0);
    }
    static bool CanReach(Control target,Form window)
    {
        var sample=target.ClientRectangle;
        if(target is DataGridView)sample.Height=Math.Min(sample.Height,80);
        // Wide editable fields/grids can span a scrollable canvas; verify access to both ends.
        bool wide=sample.Width>120 && !target.Name.StartsWith("btn");
        if(!wide)return CanReachRectangle(target,window,sample);
        int right=sample.Right;
        sample.Width=120;
        if(!CanReachRectangle(target,window,sample))return false;
        sample.X=right-sample.Width;
        return CanReachRectangle(target,window,sample);
    }
    static bool CanReachRectangle(Control target,Form window,Rectangle sample)
    {
        for(Control? parent=target.Parent;parent is not null;parent=parent.Parent)
        {
            if(parent is ScrollableControl scroll&&scroll.AutoScroll)
            {
                var rectangle=scroll.RectangleToClient(target.RectangleToScreen(sample));
                int dx=rectangle.Left<0?rectangle.Left:Math.Max(0,rectangle.Right-scroll.ClientSize.Width);
                int dy=rectangle.Top<0?rectangle.Top:Math.Max(0,rectangle.Bottom-scroll.ClientSize.Height);
                var position=scroll.AutoScrollPosition;
                scroll.AutoScrollPosition=new Point(Math.Max(0,-position.X+dx),Math.Max(0,-position.Y+dy));
            }
        }
        Rectangle visible=target.RectangleToScreen(sample);
        for(Control? parent=target.Parent;parent is not null;parent=parent.Parent)
            if(!parent.RectangleToScreen(parent.ClientRectangle).Contains(visible))return false;
        return window.RectangleToScreen(window.ClientRectangle).Contains(visible);
    }
    static bool HasClickHandler(Control control)
    {
        var key = typeof(Control).GetField("s_clickEvent", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
        return events[key] is not null;
    }
    static async Task CheckRecordEditorSelection(Control page, DataGridView grid, string name, int width)
    {
        var field = name switch
        {
            "Branches" => ("txtBranchId", "BranchID", "txtBranchName", "ClearBranchForm"),
            "StudyYears" => ("txtStudyYearId", "StudyYearID", "txtYearName", "ClearStudyYearForm"),
            "Sections" => ("txtSectionId", "SectionID", "txtSectionName", "ClearSectionForm"),
            "Subjects" => ("txtSubjectId", "SubjectID", "txtSubjectName", "ClearSubjectForm"),
            "Classrooms" => ("txtClassroomId", "ClassroomID", "txtClassroomNumber", "ClearClassroomForm"),
            "TimeSlots" => ("txtTimeSlotId", "TimeSlotID", "", "ClearTimeSlotForm"),
            "FacultyMembers" => ("txtFacultyMemberId", "FacultyMemberID", "txtFacultyMemberFullName", "ClearFacultyMemberForm"),
            _ => default
        };
        if (field.Item1 is null) return;
        var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => !r.IsNewRow);
        if (row is null) throw new InvalidOperationException("The isolated fixture needs a record to verify editor selection.");
        var column = grid.Columns.Cast<DataGridViewColumn>().Single(c => c.DataPropertyName == field.Item2);
        string expected = row.Cells[column.Index].Value!.ToString()!;
        var id = page.Controls.Find(field.Item1, true).Single();
        grid.CurrentCell = row.Cells[grid.Columns.Cast<DataGridViewColumn>().First(c => c.Visible).Index];
        row.Selected = true;
        await Task.Delay(30);
        Check($"{name} grid selection retains the edit/delete record ID at {width}", id.Text == expected);
        if (field.Item3 != "")
        {
            var input = page.Controls.Find(field.Item3, true).Single();
            input.Text = "Local unsaved edit";
            id.Focus();
            input.Focus();
            await Task.Delay(150);
            Check($"{name} leaving the read-only ID preserves unsaved edits at {width}", input.Text == "Local unsaved edit");
        }
        page.GetType().GetMethod(field.Item4, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(page, null);
    }
    static async Task CheckPendingEditorClear(Control page)
    {
        var grid=(DataGridView)page.Controls.Find("dgvSchedules",true).Single();
        if(grid.Rows.Count==0)throw new InvalidOperationException("A saved lesson is needed for the editor race check.");
        grid.CurrentCell=grid.Rows[0].Cells.Cast<DataGridViewCell>().First(c=>c.Visible);
        grid.Rows[0].Selected=true;
        await Task.Delay(250);
        var connectionType=Assembly.Load("Microsoft.Data.SqlClient").GetType("Microsoft.Data.SqlClient.SqlConnection")!;
        await using var connection=(System.Data.Common.DbConnection)Activator.CreateInstance(connectionType,
            [Environment.GetEnvironmentVariable("UNIVERSITY_TIMETABLE_CONNECTION_STRING")!])!;
        await connection.OpenAsync();
        await using var transaction=await connection.BeginTransactionAsync();
        using(var command=connection.CreateCommand())
        {
            command.Transaction=transaction;
            command.CommandText="SELECT TOP (1) 1 FROM Schedules WITH (TABLOCKX,HOLDLOCK)";
            await command.ExecuteScalarAsync();
        }
        var pending=(Task)page.GetType().GetMethod("PopulateScheduleEditorFromSelectionAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(page,null)!;
        Check("Selected schedule loading is deferred for the clear-form race",!pending.IsCompleted);
        page.GetType().GetMethod("ClearScheduleForm",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(page,null);
        await transaction.CommitAsync();
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Check("Clearing the schedule form prevents an older read from repopulating it",
            page.Controls.Find("txtScheduleId",true).Single().Text=="" &&
            ((ComboBox)page.Controls.Find("cmbSubject",true).Single()).SelectedIndex<0 &&
            ((ComboBox)page.Controls.Find("cmbFacultyMember",true).Single()).SelectedIndex<0);
    }
    static async Task EnsureSelectionFixture()
    {
        // The existing isolated fixture may contain only general-year subjects
        // and no branch. Seed that one missing record through the real service.
        var business = Assembly.Load("Business_Logic_Layer");
        var service = Activator.CreateInstance(business.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.BranchService")!)!;
        var read = (Task)service.GetType().GetMethod("GetAllAsync")!.Invoke(service, null)!;
        await read;
        var records = (System.Collections.IEnumerable)read.GetType().GetProperty("Result")!.GetValue(read)!;
        if (records.Cast<object>().Any()) return;
        var add = service.GetType().GetMethod("AddAsync")!;
        var branch = Activator.CreateInstance(add.GetParameters()[0].ParameterType)!;
        branch.GetType().GetProperty("BranchName")!.SetValue(branch, "Cleanup regression branch");
        await (Task)add.Invoke(service, [branch])!;
    }
    static void CheckTimeSlotDefaults(Control page)
    {
        var start=page.Controls.Find("dtpStartTime",true).Single();
        var end=page.Controls.Find("dtpEndTime",true).Single();
        var value=start.GetType().GetProperty("Value")!;
        var period = ((TimeSpan Start, TimeSpan End))Assembly.Load("Business_Logic_Layer")
            .GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.TimeSlotService")!
            .GetProperty("DefaultLecturePeriod")!.GetValue(null)!;
        Check("Time-slot editors use the configured default lecture period", ((DateTime)value.GetValue(start)!).TimeOfDay==period.Start && ((DateTime)value.GetValue(end)!).TimeOfDay==period.End);
        var hint=page.Controls.Find("lblPageSubtitle",true).Single();
        Check("The flexible-duration hint is visible and fits the header", hint.Visible && hint.Parent!.ClientRectangle.Contains(hint.Bounds) && hint.Text.Contains("مدة المحاضرة مرنة"));
        value.SetValue(start,DateTime.Today.AddHours(13).AddMinutes(30).AddSeconds(35));
        value.SetValue(end,DateTime.Today.AddHours(15).AddSeconds(35));
        object?[] arguments=[null,false];
        bool accepted=(bool)page.GetType().GetMethod("TryBuildTimeSlot",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(page,arguments)!;
        var slot=arguments[0]!;
        Check("An edited 90-minute afternoon period is accepted with minute precision", accepted && (TimeSpan)slot.GetType().GetProperty("StartTime")!.GetValue(slot)! == new TimeSpan(13,30,0) && (TimeSpan)slot.GetType().GetProperty("EndTime")!.GetValue(slot)! == new TimeSpan(15,0,0));
        page.GetType().GetMethod("ClearTimeSlotForm",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(page,null);
    }
    static void CheckPasswordToggle(Form form, string buttonName, string inputName)
    {
        var button = form.Controls.Find(buttonName, true).Single();
        var input = form.Controls.Find(inputName, true).Single();
        var password = input.GetType().GetProperty("PasswordChar")!;
        var image = button.GetType().GetProperty("Image")!;
        object? initialImage = image.GetValue(button);
        bool initiallyHidden = (char)password.GetValue(input)! == '*';
        button.GetType().GetMethod("PerformClick")!.Invoke(button, null);
        Check(form.GetType().Name + " " + buttonName + " reveals the password and updates its icon",
            initiallyHidden && (char)password.GetValue(input)! == '\0' && button.AccessibleName == "إخفاء كلمة المرور" && image.GetValue(button) != null && !ReferenceEquals(initialImage, image.GetValue(button)));
        button.GetType().GetMethod("PerformClick")!.Invoke(button, null);
        Check(form.GetType().Name + " " + buttonName + " hides the password again", (char)password.GetValue(input)! == '*' && button.AccessibleName == "إظهار كلمة المرور");
    }
    static IEnumerable<Control> Children(Control parent)
    {
        foreach(Control child in parent.Controls)
        {
            yield return child;
            if(child is Panel or GroupBox or UserControl || child.GetType().Name=="Guna2Panel")
                foreach(var descendant in Children(child))yield return descendant;
        }
    }
    static void Check(string name,bool pass)
    {
        results.Add(new{name,passed=pass});if(!pass)failures++;
        Console.WriteLine((pass?"PASS ":"FAIL ")+name);
    }
    static void Save(Control control,string name)
    {
        using var bitmap=new Bitmap(control.Width,control.Height);
        control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,control.Size));
        bitmap.Save(Path.Combine(output,name+".png"));
        File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(Children(control).Where(c=>!string.IsNullOrEmpty(c.Name)).Select(c=>new{c.Name,type=c.GetType().Name,parent=c.Parent?.Name,c.Text,c.Bounds,c.Visible,c.Enabled,font=c.Font.ToString(),back=c.BackColor.ToArgb(),fore=c.ForeColor.ToArgb(),columns=c is DataGridView grid?grid.Columns.Cast<DataGridViewColumn>().Select(col=>new{col.Name,col.HeaderText,col.Visible,col.Width,col.DisplayIndex}).ToArray():null}),new JsonSerializerOptions{WriteIndented=true}));
    }
    static void CheckWeeklySelectionAndScrolling(Control page,DataGridView weekly)
    {
        var rowsField=page.GetType().GetField("scheduleRows",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var rows=(System.Collections.IEnumerable)rowsField.GetValue(page)!;
        object[] entries=rows.Cast<object>().Take(1).ToArray();
        if(entries.Length==0)return;
        int id=(int)entries[0].GetType().GetProperty("ScheduleID")!.GetValue(entries[0])!;
        string day=(string)entries[0].GetType().GetProperty("DayOfWeek")!.GetValue(entries[0])!;
        int col=weekly.Columns.Cast<DataGridViewColumn>().Single(c=>c.DataPropertyName==day).Index;
        var cellType=page.GetType().GetNestedType("TimetableCell",BindingFlags.NonPublic)!;
        var listType=typeof(List<>).MakeGenericType(entries[0].GetType());
        var list=(System.Collections.IList)Activator.CreateInstance(listType)!;
        for(int i=0;i<4;i++)list.Add(entries[0]);
        var originalCell = weekly.Rows[0].Cells[col].Tag;
        weekly.Rows[0].Cells[col].Tag=Activator.CreateInstance(cellType,list,new HashSet<int>());
        using var headerBefore = new Bitmap(weekly.Width, weekly.Height);
        weekly.DrawToBitmap(headerBefore, weekly.ClientRectangle);
        var bounds=weekly.GetCellDisplayRectangle(col,0,false);
        var scroll=page.GetType().GetMethod("ScrollTimetableCell",BindingFlags.NonPublic|BindingFlags.Instance)!;
        Check("Crowded weekly cells can scroll",(bool)scroll.Invoke(page,[new MouseEventArgs(MouseButtons.None,0,bounds.X+10,bounds.Y+15,-120)])!);
        using var headerAfter = new Bitmap(weekly.Width, weekly.Height);
        weekly.DrawToBitmap(headerAfter, weekly.ClientRectangle);
        bool headerUnchanged = true;
        for (int y = 0; y < weekly.ColumnHeadersHeight && headerUnchanged; y++)
            for (int x = 0; x < weekly.Width; x++)
                if (headerBefore.GetPixel(x, y) != headerAfter.GetPixel(x, y)) { headerUnchanged = false; break; }
        Check("Scrolling weekly cards keeps text out of the header", headerUnchanged);
        var click=page.GetType().GetMethod("ClickTimetableCell",BindingFlags.NonPublic|BindingFlags.Instance)!;
        click.Invoke(page,[weekly,new DataGridViewCellMouseEventArgs(col,0,10,15,new MouseEventArgs(MouseButtons.Left,1,10,15,0))]);
        var grid=(DataGridView)page.Controls.Find("dgvSchedules",true).Single();
        int selected=(int)grid.CurrentRow!.DataBoundItem!.GetType().GetProperty("ScheduleID")!.GetValue(grid.CurrentRow.DataBoundItem)!;
        Check("Weekly card selection selects its schedule",selected==id);
        weekly.Rows[0].Cells[col].Tag = originalCell;
        ((System.Collections.IDictionary)page.GetType().GetField("timetableCellOffsets", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!).Clear();
        weekly.Invalidate();
    }
}
