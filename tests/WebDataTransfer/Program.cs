using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using University_Timetable_and_Classroom_Management_System.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

// Full restore and generation exercise only this uniquely named, disposable database.
string database = "UniversityTimetable_WebTransfer_" + Guid.NewGuid().ToString("N");
var settings = new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString) { InitialCatalog = database };
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(settings.ConnectionString).Options;
Process? host = null;
using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() });
string? csrf = null;
int passed = 0;
try
{
    await using (var db = new AppDbContext(options))
    {
        await db.Database.EnsureCreatedAsync();
        db.StudyYears.Add(new() { StudyYearID = 1, YearName = "First Year" });
        db.Sections.Add(new() { SectionID = 1, SectionName = "A", StudentCount = 30, StudyYearID = 1 });
        db.FacultyMembers.Add(new() { FacultyMemberID = 1, FullName = "Test Faculty" });
        db.Classrooms.Add(new() { ClassroomID = 1, ClassroomNumber = "Room 1", Capacity = 60, RoomType = "Lecture" });
        db.TimeSlots.Add(new() { TimeSlotID = 1, StartTime = new(8,30,0), EndTime = new(10,10,0) });
        for (int term=1;term<=2;term++)
        {
            db.Subjects.Add(new() { SubjectID=term,SubjectName="Subject "+term,StudyYearID=1,SemesterNumber=term,TheoreticalHours=2,TheoreticalLectureCount=1,PracticalLectureCount=0,CreditUnits=2 });
            db.FacultyMemberSubjects.Add(new() { FacultyMemberID=1,SubjectID=term });
        }
        await db.SaveChangesAsync();
        for (int term=1;term<=2;term++)db.Schedules.Add(new() { SubjectID=term,FacultyMemberID=1,ClassroomID=1,TimeSlotID=1,DayOfWeek="Sunday",LectureType="Theory",SectionID=1,StudyYearID=1,SemesterNumber=term,IsLocked=term==2 });
        await db.SaveChangesAsync();
    }
    string password=Guid.NewGuid()+"a!7";
    var previousConnection=Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
    try {
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,settings.ConnectionString);
        var seeded=await new AuthService().RegisterAsync("Transfer test","transfer",password,password);
        Check("Administrator seeded outside public API",seeded.Succeeded && seeded.User!.ApplicationUserID==1);
    } finally { Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,previousConnection); }
    var listener = new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
    client.BaseAddress = new Uri($"http://localhost:{port}");
    string api = Path.GetFullPath("UniversityTimetable.API");
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory=api,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
    start.ArgumentList.Add(Path.Combine(api,"bin/Debug/net8.0/UniversityTimetable.API.dll"));start.ArgumentList.Add("--urls");start.ArgumentList.Add(client.BaseAddress.ToString());
    start.Environment[DatabaseConnection.EnvironmentVariableName]=settings.ConnectionString;
    start.Environment["ConnectionStrings__UniversityTimetable"]=settings.ConnectionString;
    host=Process.Start(start)!;host.OutputDataReceived+=(_,_)=>{};host.ErrorDataReceived+=(_,_)=>{};host.BeginOutputReadLine();host.BeginErrorReadLine();
    bool ready=false;
    for(int i=0;i<100;i++){if(host.HasExited)throw new Exception("Isolated API failed to start.");try{ready=(await client.GetAsync("/")).IsSuccessStatusCode;if(ready)break;}catch(HttpRequestException){}await Task.Delay(100);}
    Check("Isolated API starts",ready);
    csrf=(await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();

    await Send("auth/register",new{fullName="Blocked",userName="blocked",password,confirmPassword=password},401);
    await Send("auth/login",new{userName="transfer",password},200);
    csrf=(await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();
    byte[] backup=await client.GetByteArrayAsync("/api/timetable-backups/download");
    var preview=await Upload("timetable-backups/preview",backup,"snapshot.json");
    Check("Downloaded backup is restorable",preview["preview"]!["canRestore"]!.GetValue<bool>());
    await using(var db=new AppDbContext(options))await db.Schedules.Where(s=>s.SemesterNumber==1).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DayOfWeek,"Monday"));
    await Send("timetable-backups/restore",new{id=preview["id"]!.GetValue<string>(),confirmed=true},409);
    var daySettings=new{facultyId=1,semester=1,mode=0,selectedDays=Array.Empty<string>(),timeLimitSeconds=5,useDayPreferences=true,
        dayPreferences=new[]{"Sunday","Monday","Tuesday","Wednesday","Thursday"}.Select(d=>new{day=d,level=d=="Tuesday"?0:d=="Sunday"?2:1}).ToArray()};
    await Send("faculty-preferences/days",new{settings=daySettings,expectedRevision=0,confirmed=false},400,HttpMethod.Put);
    var dayResult=await Send("faculty-preferences/days",new{settings=daySettings,expectedRevision=0,confirmed=true},200,HttpMethod.Put);
    long dayRevision=dayResult["profile"]!["revision"]!.GetValue<long>();
    await using(var db=new AppDbContext(options))Check("Day preferences atomically optimize the timetable",await db.Schedules.AnyAsync(s=>s.SemesterNumber==1&&s.DayOfWeek=="Tuesday")&&await db.Schedules.AnyAsync(s=>s.SemesterNumber==2&&s.IsLocked&&s.DayOfWeek=="Sunday"));
    await Send("faculty-preferences/days",new{settings=daySettings,expectedRevision=0,confirmed=true},409,HttpMethod.Put);
    await Send("faculty-preferences/1/1",new{expectedRevision=dayRevision,confirmed=true},204,HttpMethod.Delete);
    var noProfile=await client.GetFromJsonAsync<JsonObject>("/api/faculty-preferences/1/1");
    Check("Optional preferences can be removed",noProfile!["profile"] is null);
    preview=await Upload("timetable-backups/preview",backup,"snapshot.json");
    var result=await Send("timetable-backups/restore",new{id=preview["id"]!.GetValue<string>(),confirmed=true},200);
    await using(var db=new AppDbContext(options))Check("Restore replaces both semesters and retains locks",await db.Schedules.CountAsync()==2&&await db.Schedules.AllAsync(s=>s.DayOfWeek=="Sunday")&&await db.Schedules.AnyAsync(s=>s.SemesterNumber==2&&s.IsLocked));
    byte[] safety=await client.GetByteArrayAsync(result["safetyDownload"]!.GetValue<string>());
    using var other=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer()}){BaseAddress=client.BaseAddress};
    string otherToken=(await other.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();
    other.DefaultRequestHeaders.Add("X-CSRF-TOKEN",otherToken);
    await Send("auth/register",new{fullName="Other",userName="other",password,confirmPassword=password},201);
    Check("Second account login",(await other.PostAsJsonAsync("/api/auth/login",new{userName="other",password})).IsSuccessStatusCode);
    other.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");other.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(await other.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>());
    Check("Safety download belongs to its account",(await other.GetAsync(result["safetyDownload"]!.GetValue<string>())).StatusCode==HttpStatusCode.NotFound);
    var undo=await Upload("timetable-backups/preview",safety,"undo.json");
    Check("Safety backup can undo restore",undo["preview"]!["canRestore"]!.GetValue<bool>());
    await Send("timetable-backups/restore",new{id=undo["id"]!.GetValue<string>(),confirmed=true},200);
    await using(var db=new AppDbContext(options))Check("Undo restores the previous placement",await db.Schedules.AnyAsync(s=>s.SemesterNumber==1&&s.DayOfWeek=="Tuesday"));
    await Send("timetable-backups/restore",new{id=preview["id"]!.GetValue<string>(),confirmed=true},409);
    using var workbook=new XLWorkbook();var sheet=workbook.Worksheets.Add("Names");
    sheet.Cell(1,1).Value="اسم التدريسي";sheet.Cell(1,2).Value="المادة";
    sheet.Cell(2,1).Value="Transfer Teacher";sheet.Cell(2,2).Value="Transfer Subject";
    using var stream=new MemoryStream();workbook.SaveAs(stream);
    var upload=await Upload("imports/upload",stream.ToArray(),"names.xlsx");
    string id=upload["id"]!.GetValue<string>();var source=upload["sheets"]![0]!;
    var input=new{index=0,schema="NamedAssignments",mapping=source["mappings"]!["NamedAssignments"]!.DeepClone(),rows=source["rows"]!.DeepClone()};
    await Send("imports/validate",new{id,mode=2,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{input}},400);
    await Send("imports/validate",new{id,mode=3,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{input}},400);
    var validation=await Send("imports/validate",new{id,mode=0,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{input}},200);
    Check("Academic import preview is valid",validation["preview"]!["isValid"]!.GetValue<bool>());
    Check("Reader cannot commit an import",(await other.PostAsJsonAsync("/api/imports/save",new{id,revision=validation["revision"]!.GetValue<string>(),confirmed=true})).StatusCode==HttpStatusCode.Forbidden);
    await Send("imports/save",new{id,revision=validation["revision"]!.GetValue<string>(),confirmed=false},400);
    await Send("imports/save",new{id,revision=validation["revision"]!.GetValue<string>(),confirmed=true},200);
    await using(var db=new AppDbContext(options))Check("Reviewed names persisted",await db.FacultyMembers.AnyAsync(x=>x.FullName=="Transfer Teacher")&&await db.Subjects.AnyAsync(x=>x.SubjectName=="Transfer Subject"));
    using(var form=new MultipartFormDataContent()) {
        form.Add(new ByteArrayContent("%PDF-1.7 unsupported"u8.ToArray()),"file","unsupported.pdf");
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/imports/upload"){Content=form};request.Headers.Add("X-CSRF-TOKEN",csrf);
        Check("PDF import explicitly rejected",(await client.SendAsync(request)).StatusCode==HttpStatusCode.BadRequest);
    }
    Console.WriteLine($"PASS: {passed} isolated web transfer checks.");
}
finally
{
    if(host is not null){if(!host.HasExited){host.Kill(true);await host.WaitForExitAsync();}host.Dispose();}
    await using var db=new AppDbContext(options);await db.Database.EnsureDeletedAsync();
    // Remove only safety files whose payload explicitly identifies this disposable DB.
    if(Directory.Exists(TimetableBackupService.AutomaticBackupDirectory))foreach(string path in Directory.GetFiles(TimetableBackupService.AutomaticBackupDirectory,"Before-Web-Restore-*.json"))
        if(JsonNode.Parse(await File.ReadAllTextAsync(path))?["Payload"]?["Database"]?.GetValue<string>()==database)File.Delete(path);
}
void Check(string name,bool passedCheck){if(!passedCheck)throw new Exception("FAILED: "+name);passed++;Console.WriteLine("PASS "+name);}
async Task<JsonObject> Send(string route,object body,int expected,HttpMethod? method=null)
{
    using var request=new HttpRequestMessage(method??HttpMethod.Post,"/api/"+route){Content=JsonContent.Create(body)};request.Headers.Add("X-CSRF-TOKEN",csrf);
    using var response=await client.SendAsync(request);string text=await response.Content.ReadAsStringAsync();
    Check($"{route}: {expected}",(int)response.StatusCode==expected);
    return string.IsNullOrWhiteSpace(text)?new JsonObject():JsonNode.Parse(text)!.AsObject();
}
async Task<JsonObject> Upload(string route,byte[] data,string name)
{
    using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(data),"file",name);
    using var request=new HttpRequestMessage(HttpMethod.Post,"/api/"+route){Content=form};request.Headers.Add("X-CSRF-TOKEN",csrf);
    using var response=await client.SendAsync(request);string body=await response.Content.ReadAsStringAsync();
    Check("Upload "+route,response.IsSuccessStatusCode);return JsonNode.Parse(body)!.AsObject();
}
