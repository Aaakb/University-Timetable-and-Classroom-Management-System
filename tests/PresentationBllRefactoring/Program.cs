using System.Reflection;
using System.Text.Json;
using University_Timetable_and_Classroom_Management_System;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

internal static class Program
{
    private static readonly List<object> Results = [];
    private static readonly MethodInfo RunEditor = typeof(BranchesForm).Assembly
        .GetType("University_Timetable_and_Classroom_Management_System.EditorAction")!.GetMethod("RunAsync")!;
    private static int failures;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var dispatcher = new Form { Opacity = 0, ShowInTaskbar = false };
        dispatcher.Shown += async (_, _) =>
        {
            try
            {
                await CheckEditorActions();
                if (!args.Contains("presentation-only")) await CheckRecordValidation();
            }
            catch (Exception ex) { Record("Harness completes", ex); }
            finally { dispatcher.Close(); }
        };
        Application.Run(dispatcher);
        string output = Path.GetFullPath(".tmp/PresentationBllRefactoring/action-checks.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(Results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY {Results.Count} checks; {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    private static async Task CheckEditorActions()
    {
        await Check("Successful edit saves, reloads, clears and reports on the original UI thread", async () =>
        {
            using var owner = new UserControl();
            var events = new List<string>();
            int thread = Environment.CurrentManagedThreadId;
            void Add(string value)
            {
                Ensure(Environment.CurrentManagedThreadId == thread, "Continuation left the UI thread");
                events.Add(value);
            }
            await Run(owner, async () => { await Task.Yield(); Add("save"); },
                async () => { await Task.Yield(); Add("reload"); },
                enabled => Add(enabled ? "enable" : "disable"),
                () => { Add("clear"); Add("success"); }, _ => Add("error"));
            Ensure(events.SequenceEqual(new[] { "disable", "save", "reload", "clear", "success", "enable" }));
        });

        foreach (string stage in new[] { "before start", "pending save", "pending reload" })
        {
            await Check($"Navigation during {stage} prevents access to the disposed editor", async () =>
            {
                using var owner = new UserControl();
                var events = new List<string>();
                var pending = new TaskCompletionSource();
                if (stage == "before start") owner.Dispose();
                Task Save() { events.Add("save"); return stage == "pending save" ? pending.Task : Task.CompletedTask; }
                Task Reload() { events.Add("reload"); return pending.Task; }
                var run = Run(owner, Save, Reload, enabled => events.Add(enabled ? "enable" : "disable"),
                    () => throw new Exception("Touched disposed editor"), _ => events.Add("error"));
                owner.Dispose();
                pending.SetResult();
                await run;
                var expected = stage switch
                {
                    "before start" => Array.Empty<string>(),
                    "pending save" => ["disable", "save", "enable"],
                    _ => new[] { "disable", "save", "reload", "enable" }
                };
                Ensure(events.SequenceEqual(expected), string.Join(",", events));
            });
        }

        foreach (string stage in new[] { "synchronous save", "asynchronous save", "reload", "success callback" })
        {
            await Check($"Failure in {stage} preserves the original exception and restores actions", async () =>
            {
                using var owner = new UserControl();
                var expected = new InvalidOperationException("diagnostic detail");
                Exception? reported = null;
                var events = new List<string>();
                Task Save() => stage switch
                {
                    "synchronous save" => throw expected,
                    "asynchronous save" => Task.FromException(expected),
                    _ => Task.CompletedTask
                };
                await Run(owner, Save,
                    () => stage == "reload" ? Task.FromException(expected) : Task.CompletedTask,
                    enabled => events.Add(enabled ? "enable" : "disable"),
                    () => { if (stage == "success callback") throw expected; events.Add("success"); },
                    ex => { reported = ex; events.Add("error"); });
                Ensure(ReferenceEquals(reported, expected));
                Ensure(events.SequenceEqual(new[] { "disable", "error", "enable" }));
            });
        }

        await Check("An error-display failure propagates after restoring action state", async () =>
        {
            using var owner = new UserControl();
            bool restored = false;
            var expected = new InvalidOperationException("display failed");
            try
            {
                await Run(owner, () => Task.FromException(new Exception("save failed")), () => Task.CompletedTask,
                    enabled => restored = enabled, () => { }, _ => throw expected);
                throw new Exception("Error was swallowed");
            }
            catch (InvalidOperationException ex) when (ReferenceEquals(ex, expected)) { Ensure(restored); }
        });
    }

    private static async Task CheckRecordValidation()
    {
        var validate = typeof(BranchService).Assembly.GetType(
            "University_Timetable_and_Classroom_Management_System.BusinessLayer.InputValidation")!.GetMethod("RecordIdAsync")!;
        foreach (string entity in new[] { "Branch", "Classroom", "Faculty member", "Section", "Study year", "Subject", "Time slot" })
        foreach (var input in new (int Id, bool Update, bool Exists)[] { (0, false, false), (-1, true, true), (1, false, true), (1, true, false), (1, false, false), (1, true, true) })
        {
            await Check($"{entity}: id={input.Id}, update={input.Update}, exists={input.Exists}", async () =>
            {
                int queries = 0;
                Exception? rejection = null;
                try
                {
                    await (Task)validate.Invoke(null, [input.Id, input.Update,
                        (Func<Task<bool>>)(() => { queries++; return Task.FromResult(input.Exists); }), entity])!;
                }
                catch (Exception ex) { rejection = ex; }
                string? message = input.Id <= 0 ? $"{entity} ID is required."
                    : !input.Update && input.Exists ? $"{entity} ID already exists."
                    : input.Update && !input.Exists ? $"{entity} not found." : null;
                Ensure(queries == (input.Id <= 0 ? 0 : 1), "Changed existence query order/count");
                Ensure(rejection?.Message == message);
                if (message is not null)
                    Ensure(rejection!.GetType() == (input.Id > 0 && input.Update ? typeof(KeyNotFoundException) : typeof(ArgumentException)));
            });
        }
    }

    private static Task Run(Control owner, Func<Task> action, Func<Task> reload, Action<bool> enabled,
        Action success, Action<Exception> error) => (Task)RunEditor.Invoke(null, [owner, action, reload, enabled, success, error])!;

    private static void Ensure(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }

    private static async Task Check(string name, Func<Task> check)
    {
        try { await check(); Record(name, null); }
        catch (Exception ex) { Record(name, ex); }
    }

    private static void Record(string name, Exception? error)
    {
        if (error is not null) failures++;
        Results.Add(new { name, passed = error is null, error = error?.ToString() });
        Console.WriteLine($"{(error is null ? "PASS" : "FAIL")} {name}{(error is null ? "" : ": " + error.Message)}");
    }
}
