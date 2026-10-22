using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

internal static partial class Program
{
    static async Task WebSmokeAsync()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetFullPath("UniversityTimetable.API") };
        start.ArgumentList.Add(Path.GetFullPath("UniversityTimetable.API/bin/Debug/net8.0/UniversityTimetable.API.dll"));
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        using var process = Process.Start(start) ?? throw new Exception("Cannot start isolated API");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(30) };
            var timer = Stopwatch.StartNew();
            while (true) { try { await client.GetAsync("/api/auth/csrf"); break; } catch (HttpRequestException) { if (timer.Elapsed.TotalSeconds > 25 || process.HasExited) throw; await Task.Delay(100); } }
            string? csrf = null;
            Assert((await client.GetAsync("/preferences.html")).IsSuccessStatusCode, "Preference HTML must be served");
            Assert((await client.GetAsync("/js/preferences.js")).IsSuccessStatusCode, "Preference module must be served");
            async Task<JsonElement> Send(string path, HttpMethod method, object? body, HttpStatusCode expected, bool protectedRequest = true)
            {
                using var message = new HttpRequestMessage(method, "/api/" + path);
                if (body is not null) message.Content = JsonContent.Create(body);
                if (method != HttpMethod.Get && protectedRequest && csrf is not null) message.Headers.Add("X-CSRF-TOKEN", csrf);
                using var response = await client.SendAsync(message); string text = await response.Content.ReadAsStringAsync();
                Assert(response.StatusCode == expected, $"{path}: expected {expected}, got {response.StatusCode}: {text}");
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text).RootElement.Clone();
            }
            await Send("faculty-preferences/lookups", HttpMethod.Get, null, HttpStatusCode.Unauthorized);
            csrf = (await Send("auth/csrf", HttpMethod.Get, null, HttpStatusCode.OK)).GetProperty("token").GetString();
            string login = "pref" + Guid.NewGuid().ToString("N")[..10], password = "Test" + Guid.NewGuid().ToString("N");
            await Send("auth/register", HttpMethod.Post, new { fullName = "مدير اختبار", userName = login, password, confirmPassword = password }, HttpStatusCode.Created);
            await Send("auth/login", HttpMethod.Post, new { userName = login, password }, HttpStatusCode.OK);
            csrf = (await Send("auth/csrf", HttpMethod.Get, null, HttpStatusCode.OK)).GetProperty("token").GetString();
            var lookups = await Send("faculty-preferences/lookups", HttpMethod.Get, null, HttpStatusCode.OK); Assert(lookups.GetProperty("data").GetProperty("periods").GetArrayLength() == 3);
            var data = await Send("faculty-preferences/1/1", HttpMethod.Get, null, HttpStatusCode.OK);
            long revision = data.GetProperty("profile").GetProperty("revision").GetInt64();
            await Send("faculty-preferences", HttpMethod.Put, new { settings = Profile(), expectedRevision = revision }, HttpStatusCode.BadRequest, protectedRequest: false);
            var saved = await Send("faculty-preferences", HttpMethod.Put, new { settings = Profile(), expectedRevision = revision }, HttpStatusCode.OK);
            await Send("faculty-preferences", HttpMethod.Put, new { settings = Profile(), expectedRevision = revision }, HttpStatusCode.Conflict);
            var result = await Send("faculty-preferences/preview", HttpMethod.Post, Profile(), HttpStatusCode.OK);
            var preview = result.GetProperty("preview"); Assert(preview.GetProperty("canSave").GetBoolean()); Assert(!preview.TryGetProperty("plan", out _));
            string id = result.GetProperty("id").GetString()!;
            await Send("faculty-preferences/commit", HttpMethod.Post, new { id, confirmed = false }, HttpStatusCode.BadRequest);
            var committed = await Send("faculty-preferences/commit", HttpMethod.Post, new { id, confirmed = true }, HttpStatusCode.OK); Assert(committed.GetProperty("changed").GetInt32() == 2);
            await Send("faculty-preferences/commit", HttpMethod.Post, new { id, confirmed = true }, HttpStatusCode.Conflict);
            var allAlternatives = await Send("timetable-alternatives/preview", HttpMethod.Post, new { semester = 1, count = 2, timeLimitSeconds = 8 }, HttpStatusCode.OK);
            Assert(allAlternatives.GetProperty("alternatives").GetArrayLength() > 0);
            string alternativeId = allAlternatives.GetProperty("alternatives")[0].GetProperty("id").GetString()!;
            await Send("timetable-alternatives/commit", HttpMethod.Post, new { id = alternativeId, confirmed = false }, HttpStatusCode.BadRequest);
            await Send("timetable-alternatives/commit", HttpMethod.Post, new { id = alternativeId, confirmed = true }, HttpStatusCode.OK);
            await Send("timetable-alternatives/commit", HttpMethod.Post, new { id = alternativeId, confirmed = true }, HttpStatusCode.Conflict);
            var current = await Send("timetable-alternatives?semester=1", HttpMethod.Get, null, HttpStatusCode.OK);
            await Send("timetable-alternatives/locks", HttpMethod.Post, new { semester = 1, ids = new[] { initial[0].ScheduleID }, locked = true, version = current.GetProperty("version").GetString(), confirmed = false }, HttpStatusCode.BadRequest);
            results.Add(new { name = "Whole timetable web alternatives preview selection confirmation and single-use commit", passed = true }); Console.WriteLine("PASS whole timetable web alternatives");
            results.Add(new { name = "Isolated web API: authentication CSRF profile concurrency server-held preview and one-time validated commit", passed = true }); Console.WriteLine("PASS isolated web API lifecycle");
        }
        catch (Exception ex) { failed++; results.Add(new { name = "Web API", passed = false, error = ex.ToString() }); Console.WriteLine("FAIL web API: " + ex); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); await Task.WhenAll(stdout, stderr); }
    }
}
