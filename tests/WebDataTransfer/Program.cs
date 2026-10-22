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
    string password=Guid.NewGuid()+"a!7";
    await Send("auth/register",new{fullName="Transfer test",userName="transfer",password,confirmPassword=password},201);
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
    Check("Second account registration",(await other.PostAsJsonAsync("/api/auth/register",new{fullName="Other",userName="other",password,confirmPassword=password})).StatusCode==HttpStatusCode.Created);
    Check("Second account login",(await other.PostAsJsonAsync("/api/auth/login",new{userName="other",password})).IsSuccessStatusCode);
    other.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");other.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(await other.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>());
    Check("Safety download belongs to its account",(await other.GetAsync(result["safetyDownload"]!.GetValue<string>())).StatusCode==HttpStatusCode.NotFound);
    var undo=await Upload("timetable-backups/preview",safety,"undo.json");
    Check("Safety backup can undo restore",undo["preview"]!["canRestore"]!.GetValue<bool>());
    await Send("timetable-backups/restore",new{id=undo["id"]!.GetValue<string>(),confirmed=true},200);
    await using(var db=new AppDbContext(options))Check("Undo restores the previous placement",await db.Schedules.AnyAsync(s=>s.SemesterNumber==1&&s.DayOfWeek=="Tuesday"));
    await Send("timetable-backups/restore",new{id=preview["id"]!.GetValue<string>(),confirmed=true},409);
    // A timetable import starts empty to exercise actual addition and generation.
    await using(var db=new AppDbContext(options))await db.Schedules.ExecuteDeleteAsync();
    using var workbook=new XLWorkbook();var sheet=workbook.Worksheets.Add("Timetable");
    string[] columns=["Day","TimeSlot","Subject","Faculty","Classroom","Section","LectureType","SemesterNumber","Locked"];
    string[] row=["Sunday","#1","#1","#1","#1","#1","Theory","1","TRUE"];
    for(int c=0;c<columns.Length;c++){sheet.Cell(1,c+1).Value=columns[c];sheet.Cell(2,c+1).Value=row[c];}
    using var stream=new MemoryStream();workbook.SaveAs(stream);
    var upload=await Upload("imports/upload",stream.ToArray(),"timetable.xlsx");
    string id=upload["id"]!.GetValue<string>();
    var input=new{index=0,schema="Timetable",mapping=columns,rows=new[]{row}};
    var validation=await Send("imports/validate",new{id,mode=2,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{input}},200);
    Check("Another account cannot save this preview",(await other.PostAsJsonAsync("/api/imports/save",new{id,revision=validation["revision"]!.GetValue<string>(),confirmed=true})).StatusCode==HttpStatusCode.Conflict);
    Check("Hybrid preview validates source",validation["preview"]!["isValid"]!.GetValue<bool>());
    await Send("imports/save",new{id,revision=validation["revision"]!.GetValue<string>(),confirmed=true},400);
    var generated=await Send("imports/generate",new{id,revision=validation["revision"]!.GetValue<string>(),seconds=5},200);
    Check("Hybrid generation finishes without persistence",generated["preview"]!["generationCompleted"]!.GetValue<bool>());
    await using(var db=new AppDbContext(options))Check("Preview does not write schedule",await db.Schedules.CountAsync()==0);
    await Send("imports/save",new{id,revision=generated["revision"]!.GetValue<string>(),confirmed=true},200);
    var pdf=new PdfDocumentBuilder();var font=pdf.AddStandard14Font(Standard14Font.Helvetica);var pdfPage=pdf.AddPage(640,480);
    foreach(var (text,x,y) in new[]{("FacultyMemberID",40,420),("FullName",240,420),("2",40,390),("PDF Faculty",240,390)})pdfPage.AddText(text,10,new PdfPoint(x,y),font);
    upload=await Upload("imports/upload",pdf.Build(),"faculty.pdf");id=upload["id"]!.GetValue<string>();
    var pdfSheet=upload["sheets"]![0]!;
    var pdfInput=new{index=0,schema="FacultyMembers",mapping=pdfSheet["mappings"]!["FacultyMembers"]!.DeepClone(),rows=pdfSheet["rows"]!.DeepClone()};
    validation=await Send("imports/validate",new{id,mode=0,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{pdfInput}},200);
    Check("PDF source validates for academic import",validation["preview"]!["isValid"]!.GetValue<bool>());
    await Send("imports/save",new{id,revision=validation["revision"]!.GetValue<string>(),confirmed=true},200);
    await using(var db=new AppDbContext(options))Check("PDF import persists parsed names",await db.FacultyMembers.AnyAsync(f=>f.FacultyMemberID==2&&f.FullName=="PDF Faculty"));
    await using(var db=new AppDbContext(options))Check("Reviewed import saved with manual lock",await db.Schedules.CountAsync()==1&&await db.Schedules.AllAsync(s=>s.IsLocked));
    // The same source can be checked, repaired and saved using the repair mode.
    upload=await Upload("imports/upload",stream.ToArray(),"repair.xlsx");id=upload["id"]!.GetValue<string>();
    validation=await Send("imports/validate",new{id,mode=3,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{input}},200);
    generated=await Send("imports/generate",new{id,revision=validation["revision"]!.GetValue<string>(),seconds=5},200);
    Check("Repair preview provides before and after comparison",generated["preview"]!["generationCompleted"]!.GetValue<bool>()&&generated["preview"]!["placementChanges"]!.AsArray().Count==1);
    await Send("imports/save",new{id,revision=generated["revision"]!.GetValue<string>(),confirmed=true},200);
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
