using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using Data_Access_Layer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

string database="UniversityTimetable_WebParity_"+Guid.NewGuid().ToString("N");
var settings=new SqlConnectionStringBuilder(DatabaseConnection.ConnectionString){InitialCatalog=database};
var dbOptions=new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(settings.ConnectionString).Options;
using var client=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer()});
Process? host=null;string csrf="";
void Check(string name,bool condition){if(!condition)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
async Task<JsonObject> Send(string path, HttpMethod method, object body, HttpStatusCode expected, string? version=null){
    using var request=new HttpRequestMessage(method,"/api/"+path){Content=JsonContent.Create(body)};
    request.Headers.Add("X-CSRF-TOKEN",csrf);if(version is not null)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");
    using var response=await client.SendAsync(request);string text=await response.Content.ReadAsStringAsync();
    Check(path+" returns "+(int)expected,response.StatusCode==expected);
    return string.IsNullOrWhiteSpace(text)?new JsonObject():JsonNode.Parse(text)!.AsObject();
}
try{
    await using(var db=new AppDbContext(dbOptions)){
        await db.Database.EnsureCreatedAsync();
        await (Task)typeof(DatabaseMaintenanceService).GetMethod("RecreateScheduleDetailsViewAsync",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[db])!;
        db.StudyYears.Add(new StudyYear{StudyYearID=1,YearName="First Year"});
        db.StudyYears.Add(new StudyYear{StudyYearID=2,YearName="Second Year"});
        db.Subjects.Add(new Subject{SubjectID=1,SubjectName="Existing subject",StudyYearID=1,SemesterNumber=1,TheoreticalLectureCount=1,PracticalLectureCount=1,MergePracticalGroups=true});
        await db.SaveChangesAsync();
    }
    string password=Guid.NewGuid()+"a!7";
    var previousConnection=Environment.GetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName);
    try {
        Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,settings.ConnectionString);
        var seeded=await new AuthService().RegisterAsync("Parity test","parity",password,password);
        Check("Administrator seeded outside public API",seeded.Succeeded && seeded.User!.ApplicationUserID==1);
    } finally { Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName,previousConnection); }
    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
    client.BaseAddress=new Uri($"http://localhost:{port}");string api=Path.GetFullPath("UniversityTimetable.API");
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=api,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    start.ArgumentList.Add(Path.Combine(api,"bin/Debug/net8.0/UniversityTimetable.API.dll"));start.ArgumentList.Add("--urls");start.ArgumentList.Add(client.BaseAddress.ToString());
    start.Environment[DatabaseConnection.EnvironmentVariableName]=settings.ConnectionString;start.Environment["ConnectionStrings__UniversityTimetable"]=settings.ConnectionString;
    host=Process.Start(start)!;host.OutputDataReceived+=(_,_)=>{};host.ErrorDataReceived+=(_,_)=>{};host.BeginOutputReadLine();host.BeginErrorReadLine();
    bool ready=false;for(int i=0;i<100;i++){if(host.HasExited)throw new Exception("Isolated API exited.");try{ready=(await client.GetAsync("/")).IsSuccessStatusCode;if(ready)break;}catch(HttpRequestException){}await Task.Delay(100);}
    Check("isolated API starts",ready);csrf=(await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();
    await Send("auth/register",HttpMethod.Post,new{fullName="Blocked",userName="blocked",password,confirmPassword=password},HttpStatusCode.Unauthorized);
    await Send("auth/login",HttpMethod.Post,new{userName="parity",password},HttpStatusCode.OK);csrf=(await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();
    await Send("auth/register",HttpMethod.Post,new{fullName="Reader",userName="reader",password,confirmPassword=password},HttpStatusCode.Created);
    await Send("auth/login",HttpMethod.Post,new{userName="reader",password},HttpStatusCode.OK);
    csrf=(await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();
    Check("Reader cannot access student names",(await client.GetAsync("/api/students/1")).StatusCode==HttpStatusCode.Forbidden);
    Check("Reader can read subjects",(await client.GetAsync("/api/subjects")).IsSuccessStatusCode);
    await Send("branches",HttpMethod.Post,new{branchName="Forbidden"},HttpStatusCode.Forbidden);
    await Send("subjects/1",HttpMethod.Put,new{subjectName="Forbidden"},HttpStatusCode.Forbidden);
    await Send("subjects/1",HttpMethod.Delete,new{},HttpStatusCode.Forbidden);
    await Send("schedules/generate",HttpMethod.Post,new{confirmReplacement=true,version="invalid"},HttpStatusCode.Forbidden);
    await Send("auth/register",HttpMethod.Post,new{fullName="Denied",userName="denied",password,confirmPassword=password},HttpStatusCode.Forbidden);
    await Send("auth/login",HttpMethod.Post,new{userName="parity",password},HttpStatusCode.OK);
    csrf=(await client.GetFromJsonAsync<JsonObject>("/api/auth/csrf"))!["token"]!.GetValue<string>();
    await Send("auth/register",HttpMethod.Post,new{fullName="Weak",userName="weak",password="abcd",confirmPassword="abcd"},HttpStatusCode.BadRequest);
    var student=await Send("students/1",HttpMethod.Post,new{fullName="Web test student"},HttpStatusCode.OK);
    int studentId=student["studentID"]!.GetValue<int>();
    await Send("students/1/move",HttpMethod.Post,new{studentIds=new[]{studentId},promote=true},HttpStatusCode.OK);
    var secondStage=(await client.GetFromJsonAsync<JsonObject>("/api/students/2"))!;
    Check("Promoted student appears in the next stage",secondStage["students"]!.AsArray().Any(x=>x!["studentID"]!.GetValue<int>()==studentId));
    using(var lists=new XLWorkbook())
    {
        var listSheet=lists.Worksheets.Add("Subjects");
        listSheet.Cell(1,1).Value="المادة";listSheet.Cell(1,2).Value="المرحلة";
        listSheet.Cell(2,1).Value="Web pending subject";listSheet.Cell(2,2).Value="First Year";
        using var file=new MemoryStream();lists.SaveAs(file);
        using var listUpload=new MultipartFormDataContent();
        listUpload.Add(new ByteArrayContent(file.ToArray()),"file","subjects.xlsx");
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/list-imports/subjects"){Content=listUpload};
        request.Headers.Add("X-CSRF-TOKEN",csrf);
        using var result=await client.SendAsync(request);
        Check("Subject list upload succeeds",result.IsSuccessStatusCode);
    }
    await using(var db=new AppDbContext(dbOptions))
        Check("List import leaves the subject semester pending",await db.Subjects.AnyAsync(x=>x.SubjectName=="Web pending subject"&&x.SemesterNumber==0));
    var generation=(await client.GetFromJsonAsync<JsonObject>("/api/schedules/generation-state?semesterNumber=1"))!;
    await Send("schedules/generate",HttpMethod.Post,new{confirmReplacement=true,version=generation["version"]!.GetValue<string>(),semesterNumber=2},HttpStatusCode.PreconditionFailed);
    await using(var pagingDb=new AppDbContext(dbOptions)) {
        pagingDb.Branches.AddRange(Enumerable.Range(1,205).Select(i=>new Branch{BranchID=i,BranchName="Paging "+i}));await pagingDb.SaveChangesAsync();
    }
    var paged=(await client.GetFromJsonAsync<JsonObject>("/api/branches?page=2&pageSize=200"))!;
    Check("SQL pagination preserves total and stable boundaries",paged["total"]!.GetValue<int>()==205&&paged["items"]!.AsArray().Count==5&&paged["items"]![0]!["data"]!["branchID"]!.GetValue<int>()==201);
    var searched=(await client.GetFromJsonAsync<JsonObject>("/api/branches?q=Paging%20205"))!;
    Check("SQL search finds records outside the first page",searched["total"]!.GetValue<int>()==1);
    Check("Invalid pagination is rejected",(await client.GetAsync("/api/branches?pageSize=201")).StatusCode==HttpStatusCode.BadRequest);
    await using(var pagingDb=new AppDbContext(dbOptions))await pagingDb.Branches.ExecuteDeleteAsync();
    var subject=(await client.GetFromJsonAsync<JsonObject>("/api/subjects/1"))!;
    Check("API exposes merged practical setting",subject["data"]!["mergePracticalGroups"]!.GetValue<bool>());
    var oldBody=subject["data"]!.DeepClone().AsObject();oldBody.Remove("mergePracticalGroups");oldBody["subjectName"]="Updated by older client";
    var updated=await Send("subjects/1",HttpMethod.Put,oldBody,HttpStatusCode.OK,subject["version"]!.GetValue<string>());
    Check("older client preserves merged practical setting",updated["data"]!["mergePracticalGroups"]!.GetValue<bool>());
    using var workbook=new XLWorkbook();var sheet=workbook.Worksheets.Add("Subjects");sheet.Cell(1,1).Value="اسم التدريسي";sheet.Cell(1,2).Value="المادة";sheet.Cell(2,1).Value="Test Imported Teacher";sheet.Cell(2,2).Value="Test Imported Subject";
    using var stream=new MemoryStream();workbook.SaveAs(stream);
    using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(stream.ToArray()),"file","subjects.xlsx");
    using var upload=new HttpRequestMessage(HttpMethod.Post,"/api/imports/upload"){Content=form};upload.Headers.Add("X-CSRF-TOKEN",csrf);
    using var uploadResponse=await client.SendAsync(upload);Check("Excel upload succeeds",uploadResponse.IsSuccessStatusCode);
    var payload=(await uploadResponse.Content.ReadFromJsonAsync<JsonObject>())!;var first=payload["sheets"]![0]!;
    var input=new{index=0,schema="NamedAssignments",mapping=first["mappings"]!["NamedAssignments"]!.DeepClone(),rows=first["rows"]!.DeepClone()};
    var preview=await Send("imports/validate",HttpMethod.Post,new{id=payload["id"]!.GetValue<string>(),mode=0,semester=1,updateExisting=false,skipInvalid=false,sheets=new[]{input}},HttpStatusCode.OK);
    Check("name-only import preview is valid",preview["preview"]!["isValid"]!.GetValue<bool>());
    await Send("imports/save",HttpMethod.Post,new{id=payload["id"]!.GetValue<string>(),revision=preview["revision"]!.GetValue<string>(),confirmed=true},HttpStatusCode.OK);
    await using(var db=new AppDbContext(dbOptions)){
        Check("teacher is saved in isolated database",await db.FacultyMembers.AnyAsync(x=>x.FullName=="Test Imported Teacher"));
        Check("subject is saved unclassified",await db.Subjects.AnyAsync(x=>x.SubjectName=="Test Imported Subject"&&x.StudyYearID==Subject.UnclassifiedStudyYearId));
        var teacher=await db.FacultyMembers.SingleAsync(x=>x.FullName=="Test Imported Teacher");
        var section=new Section{SectionID=1,SectionName="A",StudyYearID=1,StudentCount=20};
        var room=new Classroom{ClassroomNumber="Test room",Capacity=40,RoomType="Lecture"};
        var slot=new TimeSlot{StartTime=new TimeSpan(8,30,0),EndTime=new TimeSpan(10,0,0)};
        var second=new Subject{SubjectName="Second semester",StudyYearID=1,SemesterNumber=2,TheoreticalLectureCount=1,PracticalLectureCount=0};
        db.Sections.Add(section);db.Classrooms.Add(room);db.TimeSlots.Add(slot);db.Subjects.Add(second);db.Subjects.Add(new Subject{SubjectID=71,SubjectName="Assignment-only subject",StudyYearID=1,SemesterNumber=1,TheoreticalLectureCount=1});await db.SaveChangesAsync();
        db.Schedules.AddRange(new Schedule{SubjectID=1,FacultyMemberID=teacher.FacultyMemberID,SectionID=section.SectionID,StudyYearID=1,ClassroomID=room.ClassroomID,TimeSlotID=slot.TimeSlotID,SemesterNumber=1,DayOfWeek="Sunday",LectureType="Theory"},
            new Schedule{SubjectID=second.SubjectID,FacultyMemberID=teacher.FacultyMemberID,SectionID=section.SectionID,StudyYearID=1,ClassroomID=room.ClassroomID,TimeSlotID=slot.TimeSlotID,SemesterNumber=2,DayOfWeek="Thursday",LectureType="Theory"});
        await db.SaveChangesAsync();
    }
    var browserStart=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    browserStart.ArgumentList.Add(Path.GetFullPath("tests/WebActualApi/browser.mjs"));
    browserStart.Environment["WEB_PARITY_URL"]=client.BaseAddress.ToString();browserStart.Environment["WEB_PARITY_PASSWORD"]=password;
    using var browser=Process.Start(browserStart)??throw new Exception("Could not start browser test.");
    var browserOutput=browser.StandardOutput.ReadToEndAsync();var browserError=browser.StandardError.ReadToEndAsync();
    await browser.WaitForExitAsync();Console.Write(await browserOutput);Console.Error.Write(await browserError);Check("real browser integration",browser.ExitCode==0);
}finally{
    if(host is not null){if(!host.HasExited){host.Kill(true);await host.WaitForExitAsync();}host.Dispose();}
    await using var db=new AppDbContext(dbOptions);await db.Database.EnsureDeletedAsync();
}
