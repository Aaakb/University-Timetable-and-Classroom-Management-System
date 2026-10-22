using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

// Load the deployed application and its native assets rather than the test
// project's dependencies: short test output paths previously hid this failure.
string application = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var resolver = new AssemblyDependencyResolver(application);
AssemblyLoadContext.Default.Resolving += (_, name) => resolver.ResolveAssemblyToPath(name) is { } path
    ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) => resolver.ResolveUnmanagedDllToPath(name) is { } path
    ? System.Runtime.InteropServices.NativeLibrary.Load(path) : IntPtr.Zero;
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(application)!, "Business_Logic_Layer.dll"));
var service = Activator.CreateInstance(assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.SchedulePdfExportService")!)!;
var rowType = assembly.GetType("University_Timetable_and_Classroom_Management_System.BusinessLayer.SchedulePdfRow")!;
var export = service.GetType().GetMethod("ExportAsync")!;
var checks = new List<object>();
int failures = 0;

object Row(string subject, string teacher, int semester = 1) => Activator.CreateInstance(rowType,
    [semester, "المرحلة الأولى", "عام", "A", "-", "Theory", subject, teacher, "قاعة ١ | Room 01", "Sunday", "08:30 AM", "09:50 AM"])!;
async Task Check(string name, object[] rows, string path)
{
    try
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType))!;
        foreach (object row in rows) list.Add(row);
        await (Task)export.Invoke(service, [path, list])!;
        byte[] bytes = await File.ReadAllBytesAsync(path);
        if (bytes.Length < 1000 || !Encoding.ASCII.GetString(bytes.AsSpan(0, 5)).Equals("%PDF-") ||
            !Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 100))).Contains("%%EOF"))
            throw new InvalidDataException("The PDF was incomplete.");
        checks.Add(new { name, passed = true, path, size = bytes.Length });
        Console.WriteLine("PASS " + name);
    }
    catch (Exception exception)
    {
        failures++;
        checks.Add(new { name, passed = false, error = exception.ToString() });
        Console.WriteLine("FAIL " + name + " " + exception);
    }
}

await Check("English", [Row("Mathematics", "Regression Teacher")], Path.Combine(output, "english.pdf"));
await Check("Arabic", [Row("الرياضيات", "أحمد علي")], Path.Combine(output, "arabic.pdf"));
await Check("Mixed Arabic and Latin", [Row("Programming برمجة 101", "د. أحمد Ali")], Path.Combine(output, "mixed.pdf"));
await Check("Empty schedule", [], Path.Combine(output, "empty.pdf"));
await Check("Multiple pages", [Row("الرياضيات", "أحمد علي"), Row("Programming", "Ali", 2)], Path.Combine(output, "multiple-pages.pdf"));
string deepOutput = Path.Combine(output, new string('p', 100));
Directory.CreateDirectory(deepOutput);
await Check("Long destination path", [Row("الرياضيات", "أحمد علي")], Path.Combine(deepOutput, "جدول.pdf"));
await Check("Repeated export", [Row("الرياضيات", "أحمد علي")], Path.Combine(output, "repeated.pdf"));
await File.WriteAllTextAsync(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new { count = checks.Count, failures, checks }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"SUMMARY {checks.Count} checks; {failures} failures");
return failures == 0 ? 0 : 1;
