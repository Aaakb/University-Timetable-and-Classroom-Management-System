using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static partial class Program
{
    static void AlternativesUi()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        using var form = new TimetableAlternativesForm(new TimetableAlternativesService(factory));
        form.Shown += async (_, _) =>
        {
            try
            {
                var controls = Descendants(form).Where(c => c.Name.Length > 0).GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.First());
                var generate = (Button)controls["generateAlternatives"]; var commit = (Button)controls["saveAlternative"];
                var original = (DataGridView)controls["alternativeOriginal"]; var comparison = (DataGridView)controls["alternativeComparison"];
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (!generate.Enabled || original.Rows.Count != 4) { await Task.Delay(50); if (timer.Elapsed.TotalSeconds > 30) throw new Exception("Alternative UI load timed out"); }
                string before = Json(await Rows()); generate.PerformClick(); timer.Restart();
                while (!commit.Enabled) { await Task.Delay(50); if (timer.Elapsed.TotalSeconds > 45) throw new Exception("Alternative UI preview timed out"); }
                Assert(comparison.Rows.Count is >= 2 and <= 4); Assert(before == Json(await Rows()), "Generating alternatives wrote data");
                foreach (var size in new[] { new Size(900, 680), new Size(1180, 850) })
                {
                    form.Size = size; await Task.Delay(100);
                    foreach (var name in new[] { "generateAlternatives", "saveAlternative", "closeAlternatives" })
                    { var control = controls[name]; Assert(form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), "Clipped action " + name); }
                    Assert(((DataGridView)controls["alternativeDetail"]).Height > 60, "Alternative detail grid is too small");
                    using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, $"alternatives-{size.Width}.png"));
                }
                comparison.CurrentCell = comparison.Rows[0].Cells[0]; Assert(!commit.Enabled, "Current timetable must not be saved as an alternative");
                results.Add(new { name = "Whole timetable alternative UI shows comparable choices at 900 and 1180 without writing", passed = true }); Console.WriteLine("PASS alternative selection UI");
            }
            catch (Exception ex) { failed++; results.Add(new { name = "Alternative UI", passed = false, error = ex.ToString() }); Console.WriteLine("FAIL alternative UI: " + ex); }
            finally { form.Close(); }
        };
        Application.Run(form);
    }
}
