using System.ComponentModel;
using System.Runtime.InteropServices;
using Google.OrTools.Sat;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer;

/// <summary>Loads the CP-SAT native runtime without Windows MAX_PATH failures.</summary>
public static class FacultySchedulingRuntime
{
    private static readonly Lazy<IntPtr> nativeHandle = new(LoadNativeRuntime, LazyThreadSafetyMode.ExecutionAndPublication);
    public static void EnsureAvailable()
    {
        if (OperatingSystem.IsWindows()) _ = nativeHandle.Value;
    }
    public static string Check()
    {
        EnsureAvailable();
        var model = new CpModel();
        var value = model.NewIntVar(0, 1, "runtime_check");
        model.Add(value == 1);
        using var solver = new CpSolver();
        solver.StringParameters = "max_time_in_seconds:2 num_search_workers:1";
        var status = solver.Solve(model);
        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible) || solver.Value(value) != 1)
            throw new InvalidOperationException("CP-SAT runtime verification failed: " + status);
        return $"CP-SAT runtime OK: {status}; architecture={RuntimeInformation.ProcessArchitecture}; base={AppContext.BaseDirectory}";
    }
    private static IntPtr LoadNativeRuntime()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException("يتطلب محرك CP-SAT في نسخة Windows الحالية تشغيل التطبيق بنواة x64.");
        const string fileName = "google-ortools-native.dll";
        string path = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", fileName);
        if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, fileName); // RID-specific publish layout
        if (!File.Exists(path))
            throw new InvalidOperationException("ملفات CP-SAT الأصلية غير موجودة. شغّل التطبيق من مجلد البناء الكامل مع مكتباته، ثم أعد المحاولة.");
        string fullPath = Path.GetFullPath(path);
        string extendedPath = fullPath.StartsWith(@"\\?\", StringComparison.Ordinal) ? fullPath :
            fullPath.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + fullPath[2..] : @"\\?\" + fullPath;
        // Keep dependency resolution scoped to the actual DLL directory and safe
        // system/application directories; do not modify PATH or DLL search globals.
        IntPtr handle = LoadLibraryExW(extendedPath, IntPtr.Zero, 0x00000100 | 0x00001000);
        if (handle == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"تعذّر تحميل مكتبات CP-SAT (Windows {error}). شغّل النسخة الكاملة المحدثة من التطبيق. " +
                new Win32Exception(error).Message, new Win32Exception(error));
        }
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(CpModel).Assembly, (name, _, _) =>
                name is "google-ortools-native" or "google-ortools-native.dll" ? handle : IntPtr.Zero);
        }
        catch
        {
            FreeLibrary(handle);
            throw;
        }
        // Retain this handle for the lifetime of the process, as required by SWIG.
        return handle;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr handle);
}
