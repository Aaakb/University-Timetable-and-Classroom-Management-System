using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using UniversityTimetable.API;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;

// This suite creates only uniquely named test-owned records in the configured DB,
// removes those exact records in finally, and compares all business rows afterwards.
// It never invokes persisted generation and never creates another database.
var baseUrl = args.FirstOrDefault(x => x.StartsWith("http")) ?? "http://localhost:5180";
var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(baseUrl) };
string marker = "PWAT" + Guid.NewGuid().ToString("N")[..8];
string password = Guid.NewGuid().ToString("N") + "a!7";
string? token = null;
var cleanup = new List<(string Resource, string Id)>();
var results = new List<string>();
var catalog = new ResourceCatalog();
if (args.Contains("--plan-current")) { await CurrentDataPlanner.Run(); return; }
if (args.Contains("--inspect"))
{
    await using var db = new AppDbContext();
    foreach (var definition in await db.Database.SqlQueryRaw<string>("SELECT definition AS [Value] FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID('Subjects'), OBJECT_ID('Sections'), OBJECT_ID('StudyYears'))").ToListAsync()) Console.WriteLine(definition);
    return;
}
var before = await Snapshot();
int? testUserId = null;
try
{
    Check("Static application serves HTML", (await client.GetAsync("/")).IsSuccessStatusCode);
    Check("Anonymous data access rejected", (await client.GetAsync("/api/classrooms")).StatusCode == HttpStatusCode.Unauthorized);
    var noCsrf = await client.PostAsJsonAsync("/api/auth/login", new { userName = marker, password });
    Check("CSRF required on login", noCsrf.StatusCode == HttpStatusCode.BadRequest);
    await Csrf();
    await Send("auth/register", HttpMethod.Post, new { fullName = marker, userName = marker, password, confirmPassword = password }, 201);
    await using (var db = new AppDbContext()) testUserId = await db.ApplicationUsers.Where(x => x.UserName == marker).Select(x => x.ApplicationUserID).SingleAsync();
    var signedIn = await Send("auth/login", HttpMethod.Post, new { userName = marker, password }, 200);
    Check("Credentials are not returned", !signedIn.ToJsonString().Contains("password", StringComparison.OrdinalIgnoreCase));
    await Csrf();
    await Send("auth/me", HttpMethod.Get, null, 200);
    foreach (string resource in catalog.Keys)
    {
        var response = await Send(resource + "?pageSize=1", HttpMethod.Get, null, 200);
        Check($"{resource}: list pagination", response["items"]!.AsArray().Count <= 1);
    }
    await Send("overview", HttpMethod.Get, null, 200);
    await Send("overview/rules", HttpMethod.Get, null, 200);
    await Send("subjects?page=0", HttpMethod.Get, null, 400);
    await Send("branches", HttpMethod.Post, new { branchName = marker, unexpected = true }, 400);
    var branch = await Create("branches", new { branchName = marker }, "branchID");
    int branchId = branch["data"]!["branchID"]!.GetValue<int>();
    var year = await Create("studyyears", new { yearName = marker }, "studyYearID");
    int yearId = year["data"]!["studyYearID"]!.GetValue<int>();
    int? branchForYear = AcademicStructureRules.UsesGeneralSections(yearId) ? null : branchId;
    var faculty = await Create("facultymembers", new { fullName = marker, academicTitle = "Test" }, "facultyMemberID");
    int facultyId = faculty["data"]!["facultyMemberID"]!.GetValue<int>();
    var room = await Create("classrooms", new { classroomNumber = marker, capacity = 50, roomType = "Lecture" }, "classroomID");
    int roomId = room["data"]!["classroomID"]!.GetValue<int>();
    var section = await Create("sections", new { sectionName = "A", studentCount = 20, studyYearID = yearId, branchID = branchForYear }, "sectionID");
    int sectionId = section["data"]!["sectionID"]!.GetValue<int>();
    var subject = await Create("subjects", new { subjectName = marker, studyYearID = yearId, branchID = branchForYear, semesterNumber = 1, theoreticalHours = 1, practicalHours = 0, creditUnits = 1, requirementType = "Department" }, "subjectID");
    int subjectId = subject["data"]!["subjectID"]!.GetValue<int>();
    var noRequirement = await Create("subjects", new { subjectName = marker + "NoRequirement", studyYearID = yearId, branchID = branchForYear, semesterNumber = 1, theoreticalHours = 1, practicalHours = 0, creditUnits = 1 }, "subjectID");
    Check("Subject creation requires no requirement field and does not expose it", noRequirement["data"]!["requirementType"] is null);

    var assignment = await Send("assignments", HttpMethod.Post, new { facultyMemberID = facultyId, subjectID = subjectId }, 201);
    cleanup.Add(("assignments", $"{facultyId}-{subjectId}"));
    Check("Assigned faculty persisted through shared BLL", (await new FacultyMemberSubjectService().GetByIdAsync(facultyId, subjectId)) is not null);

    var slots = await new TimeSlotService().GetAllAsync();
    int gap = Enumerable.Range(0, 1439).FirstOrDefault(m => slots.All(s => TimeSpan.FromMinutes(m + 1) <= s.StartTime || TimeSpan.FromMinutes(m) >= s.EndTime), -1);
    if (gap >= 0)
    {
        var slot = await Create("timeslots", new { startTime = TimeSpan.FromMinutes(gap), endTime = TimeSpan.FromMinutes(gap + 1) }, "timeSlotID");
        int slotId = slot["data"]!["timeSlotID"]!.GetValue<int>();
        // Exercise save/update with the same valid values; this remains a test-owned row.
        await Send($"timeslots/{slotId}", HttpMethod.Put, slot["data"], 200, slot["version"]!.GetValue<string>());
    }
    int schedulingSlotId = slots.FirstOrDefault()?.TimeSlotID ?? (await new TimeSlotService().GetAllAsync()).First().TimeSlotID;
    var lesson = await Create("schedules", new { subjectID = subjectId, facultyMemberID = facultyId, classroomID = roomId, timeSlotID = schedulingSlotId, sectionID = sectionId, dayOfWeek = "Sunday", lectureType = "Theory", groupName = (string?)null }, "scheduleID");
    int lessonId = lesson["data"]!["scheduleID"]!.GetValue<int>();
    Check("Manual lesson visible through desktop BLL", (await new ScheduleService().GetByIdAsync(lessonId))?.SubjectID == subjectId);
    var duplicate = await Send("schedules", HttpMethod.Post, lesson["data"], 400);
    Check("Shared scheduler conflict validation applied", duplicate["title"] is not null);
    var details = await Send($"schedules/details?sectionId={sectionId}", HttpMethod.Get, null, 200);
    Check("Filtered schedule contains only test lesson", details["items"]!.AsArray().Count == 1);
    var pdf = await client.GetAsync($"/api/schedules/export?sectionId={sectionId}");
    byte[] pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
    Check("Existing PDF renderer returns PDF", pdf.IsSuccessStatusCode && System.Text.Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray()) == "%PDF");
    Directory.CreateDirectory(".tmp/WebIntegration"); await File.WriteAllBytesAsync(".tmp/WebIntegration/test-timetable.pdf", pdfBytes);

    var modified = new { branchName = marker + "B" };
    await Send($"branches/{branchId}", HttpMethod.Put, modified, 428);
    var updated = await Send($"branches/{branchId}", HttpMethod.Put, modified, 200, branch["version"]!.GetValue<string>());
    await Send($"branches/{branchId}", HttpMethod.Put, modified, 412, branch["version"]!.GetValue<string>());
    await Send($"branches/{branchId}", HttpMethod.Delete, null, 412, branch["version"]!.GetValue<string>());
    Check("Web update visible through desktop BLL", (await new BranchService().GetByIdAsync(branchId))!.BranchName == marker + "B");
    await new BranchService().UpdateAsync(new Branch { BranchID = branchId, BranchName = marker + "C" });
    var refreshed = await Send($"branches/{branchId}", HttpMethod.Get, null, 200);
    Check("Desktop BLL update visible after API reload", refreshed["data"]!["branchName"]!.GetValue<string>() == marker + "C");
    await Send("schedules/generate", HttpMethod.Post, new { confirmReplacement = false, version = "invalid" }, 400);
    await Send("schedules/generate", HttpMethod.Post, new { confirmReplacement = true, version = "invalid" }, 412);
    Check("Generation preconditions reject without replacing real timetable", true);
    await TransferChecks(lessonId);
    PureGeneratorCheck();

    if (args.Contains("--browser"))
    {
        var start = new ProcessStartInfo("node", "tests/WebIntegration/browser.mjs") { UseShellExecute = false };
        start.Environment["PWA_TEST_USER"] = marker; start.Environment["PWA_TEST_PASSWORD"] = password; start.Environment["PWA_TEST_URL"] = baseUrl;
        using var process = Process.Start(start)!; await process.WaitForExitAsync();
        Check("Browser interaction/responsive/offline checks", process.ExitCode == 0);
    }
    foreach (var item in cleanup)
    {
        var current = await Send($"{item.Resource}/{item.Id}", HttpMethod.Get, null, 200);
        await Send($"{item.Resource}/{item.Id}", HttpMethod.Put, current["data"], 200, current["version"]!.GetValue<string>());
    }
    foreach (var item in cleanup.ToArray().Reverse())
    {
        var current = await Send($"{item.Resource}/{item.Id}", HttpMethod.Get, null, 200);
        await Send($"{item.Resource}/{item.Id}", HttpMethod.Delete, null, 204, current["version"]!.GetValue<string>());
        if (item.Resource == "subjects") await PurgeTestSubject(item.Id);
        cleanup.Remove(item);
    }
    await Send("auth/logout", HttpMethod.Post, null, 204);
    Check("Logout invalidates session cookie", (await client.GetAsync("/api/branches")).StatusCode == HttpStatusCode.Unauthorized);
}
finally
{
    // Cleanup uses shared services and exact IDs obtained from this run only.
    foreach (var item in cleanup.AsEnumerable().Reverse())
    {
        try { await catalog[item.Resource].Delete(item.Id); if (item.Resource == "subjects") await PurgeTestSubject(item.Id); }
        catch (Exception ex) { Console.Error.WriteLine($"CLEANUP FAILED {item.Resource}/{item.Id}: {ex.GetType().Name}"); }
    }
    await using var db = new AppDbContext();
    var user = await db.ApplicationUsers.SingleOrDefaultAsync(x => x.UserName == marker && (!testUserId.HasValue || x.ApplicationUserID == testUserId));
    if (user is not null) { db.ApplicationUsers.Remove(user); await db.SaveChangesAsync(); }
    Check("All existing rows unchanged after test-owned cleanup", before == await Snapshot());
    Directory.CreateDirectory(".tmp/WebIntegration");
    await File.WriteAllTextAsync(".tmp/WebIntegration/results.json", JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"PASS: {results.Count} checks. No persisted generation, schema changes, or second database.");

void Check(string name, bool passed) { if (!passed) throw new Exception("FAILED: " + name); results.Add(name); Console.WriteLine("PASS " + name); }
async Task Csrf() { var value = await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"); token = value!["token"]!.GetValue<string>(); }
async Task<JsonObject> Send(string route, HttpMethod method, object? body, int expected, string? version = null)
{
    using var request = new HttpRequestMessage(method, "/api/" + route);
    if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token);
    if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
    if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await client.SendAsync(request);
    string text = await response.Content.ReadAsStringAsync();
    if ((int)response.StatusCode != expected) throw new Exception($"{method} {route}: expected {expected}, got {(int)response.StatusCode}: {text}");
    Check($"{method} {route.Split('?')[0]} -> {expected}", true);
    return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject();
}
async Task<JsonObject> Create(string route, object body, string idField)
{
    var result = await Send(route, HttpMethod.Post, body, 201);
    cleanup.Add((route, result["data"]![idField]!.ToString())); return result;
}
async Task<string> Snapshot()
{
    var rows = new SortedDictionary<string, object>();
    foreach (var (key, resource) in catalog) rows[key] = (await resource.List()).OrderBy(resource.Key).ToArray();
    await using var db = new AppDbContext();
    rows["allScheduleColumns"] = await db.Schedules.AsNoTracking().OrderBy(x => x.ScheduleID).ToListAsync();
    rows["allTimeSlotsIncludingBreaks"] = await db.TimeSlots.AsNoTracking().OrderBy(x => x.TimeSlotID).ToListAsync();
    rows["users"] = await db.ApplicationUsers.AsNoTracking().OrderBy(x => x.ApplicationUserID).ToListAsync();
    rows["allSubjectsIncludingArchived"] = await db.Subjects.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.SubjectID).ToListAsync();
    return Versions.Of(rows);
}
async Task PurgeTestSubject(string id)
{
    await using var db = new AppDbContext();
    var subject = await db.Subjects.IgnoreQueryFilters().SingleOrDefaultAsync(s => s.SubjectID == int.Parse(id) && (s.SubjectName == marker || s.SubjectName == marker + "NoRequirement"));
    if (subject is not null) { db.Subjects.Remove(subject); await db.SaveChangesAsync(); }
}
async Task<JsonObject> Upload(string route, byte[] bytes, string name, int expected = 200)
{
    using var form = new MultipartFormDataContent();
    form.Add(new ByteArrayContent(bytes), "file", name);
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/" + route) { Content = form };
    request.Headers.Add("X-CSRF-TOKEN", token);
    using var response = await client.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();
    if ((int)response.StatusCode != expected) throw new Exception($"Upload {route}: {response.StatusCode}: {body}");
    Check($"Upload {route}: {expected}", true);
    return JsonNode.Parse(body)!.AsObject();
}
async Task TransferChecks(int lessonId)
{
    var lesson = await Send($"schedules/{lessonId}", HttpMethod.Get, null, 200);
    var locked = await Send($"schedules/{lessonId}/lock", HttpMethod.Put, new { locked = true }, 200, lesson["version"]!.GetValue<string>());
    Check("Lecture locks persisted through shared service", (await new ScheduleService().GetByIdAsync(lessonId))!.IsLocked);
    await Send($"schedules/{lessonId}/lock", HttpMethod.Put, new { locked = false }, 412, lesson["version"]!.GetValue<string>());
    await Send($"schedules/{lessonId}/lock", HttpMethod.Put, new { locked = false }, 200, locked["version"]!.GetValue<string>());
    byte[] backup = await client.GetByteArrayAsync("/api/timetable-backups/download");
    var restore = await Upload("timetable-backups/preview", backup, "test.json");
    Check("Backup download is readable by desktop format and preview counts rows", restore["preview"]!["lectureCount"]!.GetValue<int>() == (await new ScheduleService().GetAllAsync()).Count);
    await Send("timetable-backups/restore", HttpMethod.Post, new { id = restore["id"]!.GetValue<string>(), confirmed = false }, 400);
    await Send("timetable-backups/restore", HttpMethod.Post, new { id = "missing", confirmed = true }, 409);
    await Upload("timetable-backups/preview", System.Text.Encoding.UTF8.GetBytes("{}"), "invalid.json", 400);
    await Upload("imports/upload", [1,2,3], "invalid.exe", 400);
    await Upload("imports/upload", [1,2,3], "invalid.xlsx", 400);
    using var template = await client.GetAsync("/api/imports/template?mode=0");
    Check("Excel template download", template.IsSuccessStatusCode && (await template.Content.ReadAsByteArrayAsync()).Length > 100);
    int importedId;
    await using (var db = new AppDbContext()) importedId = (await db.Branches.MaxAsync(b => (int?)b.BranchID) ?? 0) + 1000;
    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add("Branches");
    sheet.Cell(1,1).Value = "BranchID"; sheet.Cell(1,2).Value = "BranchName";
    sheet.Cell(2,1).Value = importedId; sheet.Cell(2,2).Value = marker + "Import";
    sheet.Cell(3,1).Value = "invalid-id"; sheet.Cell(3,2).Value = marker + "Skip";
    using var file = new MemoryStream();workbook.SaveAs(file);
    var upload = await Upload("imports/upload", file.ToArray(), "branches.xlsx");
    var source = upload["sheets"]![0]!;
    var input = new { index = 0, schema = "Branches", mapping = new[] { "BranchID", "BranchName", "" }, rows = source["rows"]!.DeepClone() };
    string id = upload["id"]!.GetValue<string>();
    var invalid = await Send("imports/validate", HttpMethod.Post, new { id, mode = 0, semester = 1, updateExisting = false, skipInvalid = false, sheets = new[] { input } }, 200);
    Check("Import rejects invalid academic rows without writes", !invalid["preview"]!["isValid"]!.GetValue<bool>() && await new BranchService().GetByIdAsync(importedId) is null);
    var valid = await Send("imports/validate", HttpMethod.Post, new { id, mode = 0, semester = 1, updateExisting = false, skipInvalid = true, sheets = new[] { input } }, 200);
    Check("Skip invalid rows rebuilds valid preview", valid["preview"]!["isValid"]!.GetValue<bool>() && valid["preview"]!["skippedRows"]!.AsArray().Count > 0);
    string revision = valid["revision"]!.GetValue<string>();
    await Send("imports/save", HttpMethod.Post, new { id, revision, confirmed = false }, 400);
    await Send("imports/save", HttpMethod.Post, new { id, revision = "old", confirmed = true }, 409);
    await Send("imports/save", HttpMethod.Post, new { id, revision, confirmed = true }, 200);
    cleanup.Add(("branches", importedId.ToString()));
    Check("Import save persisted only reviewed valid row", (await new BranchService().GetByIdAsync(importedId))?.BranchName == marker + "Import");
    await Send("imports/save", HttpMethod.Post, new { id, revision, confirmed = true }, 409);
    Directory.CreateDirectory(".tmp/WebIntegration");
    await File.WriteAllBytesAsync(".tmp/WebIntegration/import-branches.xlsx", file.ToArray());
    await File.WriteAllBytesAsync(".tmp/WebIntegration/backup-preview.json", backup);
}
void PureGeneratorCheck()
{
    var assembly = typeof(ScheduleService).Assembly;
    const string prefix = "University_Timetable_and_Classroom_Management_System.BusinessLayer.";
    var generator = assembly.GetType(prefix + "ScheduleGenerator")!;
    var requestType = assembly.GetType(prefix + "ScheduleGenerationRequest")!;
    var requests = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(requestType))!;
    var subject = new Subject { SubjectID = 1, SubjectName = "Pure test", StudyYearID = 1, SemesterNumber = 1, TheoreticalHours = 1 };
    var section = new Section { SectionID = 1, SectionName = "A", StudyYearID = 1, StudentCount = 20 };
    var teaching = Activator.CreateInstance(assembly.GetType(prefix + "SubjectTeachingAssignment")!, subject, new List<int> { 1 })!;
    foreach (var request in (IEnumerable)generator.GetMethods().Single(m => m.Name == "CreateScheduleRequests" && m.GetParameters().Length == 2).Invoke(null, [teaching, section])!) requests.Add(request);
    var rooms = new List<Classroom> { new() { ClassroomID = 1, ClassroomNumber = "T", RoomType = "Lecture", Capacity = 30 } };
    var periods = new List<TimeSlot> { new() { TimeSlotID = 1, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9) } };
    var context = Activator.CreateInstance(assembly.GetType(prefix + "ScheduleBuildContext")!, rooms, periods)!;
    var result = generator.GetMethod("BuildPlan")!.Invoke(null, [requests, context])!;
    var schedules = (List<Schedule>)result.GetType().GetProperty("Schedules")!.GetValue(result)!;
    Check("Original generator plans synthetic lessons without database writes", schedules.Count == requests.Count && schedules.Count > 0);
    rooms[0].Capacity = 1;
    result = generator.GetMethod("BuildPlan")!.Invoke(null, [requests, context])!;
    schedules = (List<Schedule>)result.GetType().GetProperty("Schedules")!.GetValue(result)!;
    Check("Original generator rejects insufficient room capacity", schedules.Count == 0);
}

