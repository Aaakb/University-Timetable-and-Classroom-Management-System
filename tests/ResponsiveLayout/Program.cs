using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

internal static class Program
{
    static readonly List<object> checks = [];
    static int failures;
    [STAThread] static int Main(string[] args)
    {
        string app = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var resolver = new AssemblyDependencyResolver(app);
        AssemblyLoadContext.Default.Resolving += (_, name) => resolver.ResolveAssemblyToPath(name) is { } p ? AssemblyLoadContext.Default.LoadFromAssemblyPath(p) : null;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) => resolver.ResolveUnmanagedDllToPath(name) is { } p ? System.Runtime.InteropServices.NativeLibrary.Load(p) : IntPtr.Zero;
        if (args.Contains("diagnostics")) AppDomain.CurrentDomain.FirstChanceException += (_, e) => Console.WriteLine("DIAGNOSTIC " + e.Exception);
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(app);
        string database = File.ReadAllText(".tmp/FourFixes/results/database.txt").Trim();
        if (!database.StartsWith("UniversityTimetable_FourFixes_", StringComparison.Ordinal)) throw new InvalidOperationException("An isolated regression database is required.");
        Environment.SetEnvironmentVariable("UNIVERSITY_TIMETABLE_CONNECTION_STRING", $"Server=localhost;Database={database};Integrated Security=True;Encrypt=False;TrustServerCertificate=True");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        ApplicationConfiguration.Initialize();
        string[] names = ["BranchesForm", "StudyYearsForm", "SectionsForm", "SubjectsForm", "ClassroomsForm", "TimeSlotsForm", "FacultyMembersForm", "FacultyMemberSubjectsForm", "SchedulesForm", "LoginForm", "RegisterForm", "MainShellForm"];
        using var dispatcher = new Form { Opacity = 0, ShowInTaskbar = false, Size = new Size(1, 1) };
        dispatcher.Shown += async (_, _) =>
        {
            try { await RunScreens(assembly, names, args, output); }
            catch (Exception e) { Check("Harness completed", false, e.ToString()); }
            finally { dispatcher.Close(); }
        };
        Application.Run(dispatcher);
        File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(new{count=checks.Count,failures,checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SUMMARY {checks.Count} checks; {failures} failures");return failures==0?0:1;
    }
    static async Task RunScreens(Assembly assembly, string[] names, string[] args, string output)
    {
        foreach (string name in names.Where(n => args.Length < 3 || n == args[2]))
        {
            using var control = (Control)Activator.CreateInstance(assembly.GetType("University_Timetable_and_Classroom_Management_System." + name)!)!;
            using var host = control as Form ?? new Form { AutoScroll = true };
            if (control != host)
            {
                host.Controls.Add(control);
                control.Dock = DockStyle.None;
                bool arranging = false;
                host.ClientSizeChanged += (_, _) =>
                {
                    if (arranging || control.IsDisposed) return;
                    arranging = true;
                    try
                    {
                        host.AutoScrollMinSize = control.MinimumSize;
                        control.Bounds = new Rectangle(host.AutoScrollPosition,
                            new Size(Math.Max(control.MinimumSize.Width, host.ClientSize.Width), Math.Max(control.MinimumSize.Height, host.ClientSize.Height)));
                    }
                    finally { arranging = false; }
                };
            }
            host.StartPosition = FormStartPosition.Manual;
            host.Location = new Point(0, 0);
            host.Show();
            Console.WriteLine($"DISPLAY {name} DeviceDpi={host.DeviceDpi}; screen={Screen.FromControl(host).Bounds}; workingArea={Screen.FromControl(host).WorkingArea}");
            foreach (var size in new[] { new Size(1090,660),new Size(420,500),new Size(800,600),new Size(1640,1000),new Size(2280,1360),new Size(1090,660) })
            {
                Size chrome = host.Size - host.ClientSize;
                host.Size = new Size(Math.Max(host.MinimumSize.Width, size.Width + chrome.Width), Math.Max(host.MinimumSize.Height, size.Height + chrome.Height));
                host.PerformLayout(); await Task.Delay(40);
                Console.WriteLine($"WINDOW {name} requestedClient={size}; actualClient={host.ClientSize}; actualWindow={host.Size}; minimum={host.MinimumSize}");
                var all = Descendants(control).Prepend(control).ToArray();
                string key = name + " " + size.Width + "x" + size.Height + " dpi=" + host.DeviceDpi;
                var overlaps = new List<string>();
                foreach (var parent in all)
                {
                    var children = parent.Controls.Cast<Control>().Where(c => c.Visible && c is not DataGridView && c.Name != "pnlTimetableHost").ToArray();
                    for (int i=0; i<children.Length; i++) for (int j=i+1; j<children.Length; j++)
                        if (children[i].Bounds.IntersectsWith(children[j].Bounds)) overlaps.Add(children[i].Name+"/"+children[j].Name);
                }
                Check(key+" sibling controls do not overlap",overlaps.Count==0,string.Join(", ",overlaps));
                var inaccessible = new List<string>();
                foreach (var c in all.Where(c => c.Visible && c.Name.StartsWith("btn")))
                {
                    for (Control? parent=c.Parent;parent is not null;parent=parent.Parent)
                        if(parent is ScrollableControl scroll && scroll.AutoScroll)
                        {
                            var rectangle=scroll.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                            int dx=rectangle.Left<0?rectangle.Left:Math.Max(0,rectangle.Right-scroll.ClientSize.Width);
                            int dy=rectangle.Top<0?rectangle.Top:Math.Max(0,rectangle.Bottom-scroll.ClientSize.Height);
                            var position=scroll.AutoScrollPosition;
                            scroll.AutoScrollPosition=new Point(Math.Max(0,-position.X+dx),Math.Max(0,-position.Y+dy));
                        }
                    await Task.Delay(40);
                    var rect=c.RectangleToScreen(c.ClientRectangle);
                    for(Control? parent=c.Parent;parent is not null;parent=parent.Parent)
                        if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(rect)) { inaccessible.Add(c.Name+" in "+parent.Name+" target="+parent.RectangleToClient(rect)+" viewport="+parent.ClientRectangle+" scroll="+(parent as ScrollableControl)?.AutoScrollPosition+" extent="+(parent as ScrollableControl)?.DisplayRectangle); break; }
                }
                Check(key+" buttons fully reachable",inaccessible.Count==0,string.Join(", ",inaccessible));
                Check(key+" grid has usable size",all.OfType<DataGridView>().Where(c=>c.Visible).All(g=>g.Width>=200&&g.Height>=100));
                foreach(var scroll in all.OfType<ScrollableControl>().Where(c=>c.AutoScroll))scroll.AutoScrollPosition=Point.Empty;
                if(size.Width is 420 or 1090)
                {
                    File.WriteAllText(Path.Combine(output,name+"-"+size.Width+".json"),JsonSerializer.Serialize(all.Select(c=>new{c.Name,type=c.GetType().Name,parent=c.Parent?.Name,c.Bounds,c.Visible,c.AutoSize,c.Dock,c.MinimumSize}),new JsonSerializerOptions{WriteIndented=true}));
                    using var bitmap = new Bitmap(host.Width,host.Height);host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,name+"-"+size.Width+".png"));
                }
            }
            host.WindowState=FormWindowState.Maximized;await Task.Delay(40);host.WindowState=FormWindowState.Minimized;await Task.Delay(40);host.WindowState=FormWindowState.Normal;await Task.Delay(40);
            Check(name+" restores after minimize/maximize",host.WindowState==FormWindowState.Normal&&!host.IsDisposed);
            host.Close();
        }
    }
    static IEnumerable<Control> Descendants(Control c){foreach(Control child in c.Controls){yield return child;foreach(var d in Descendants(child))yield return d;}}
    static void Check(string name,bool passed,string? detail=null){checks.Add(new{name,passed,detail});if(!passed)failures++;Console.WriteLine((passed?"PASS ":"FAIL ")+name+(passed?"":" "+detail));}
    sealed class DesignContext:LicenseContext{public override LicenseUsageMode UsageMode=>LicenseUsageMode.Designtime;}
}
