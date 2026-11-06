using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using UniversityTimetable.API;
using University_Timetable_and_Classroom_Management_System.Models;

var merged = new Subject { SubjectID = 5, MergePracticalGroups = true };
var dto = SubjectDto.From(merged);
if (dto.MergePracticalGroups != true || !dto.ToEntity(5).MergePracticalGroups)
    throw new Exception("The web API must round-trip merged practical groups.");
dto.MergePracticalGroups = null;
if (!dto.ToEntity(5, existingMergePracticalGroups: true).MergePracticalGroups)
    throw new Exception("An older client must not erase an existing merged practical setting.");
dto.MergePracticalGroups = false;
if (dto.ToEntity(5, existingMergePracticalGroups: true).MergePracticalGroups)
    throw new Exception("An explicit unmerge must be respected.");
Console.WriteLine("PASS: merged practical setting round-trips, preserves omitted values, and accepts explicit changes.");

var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Authorization:AdministratorIds:0"]="7" }).Build();
if (!WebPermissions.IsAdministrator(configuration,7) || WebPermissions.IsAdministrator(configuration,1)) throw new Exception("Only configured accounts may administer the web application.");
Console.WriteLine("PASS: administrator allowlist does not elevate unlisted accounts.");
var logger=new AuditLogger();
var context=new DefaultHttpContext { RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider() };
context.Response.Body=new MemoryStream();
const string sensitive="PRIVATE_DATABASE_VALUE_MUST_NOT_BE_LOGGED";
var errors=new ApiErrors(_=>throw new InvalidOperationException(sensitive),logger);
await errors.InvokeAsync(context);
if(context.Response.StatusCode!=500 || !logger.Entries.Any(x=>x.Contains("stack") && x.Contains(nameof(InvalidOperationException))) || logger.Entries.Any(x=>x.Contains(sensitive))) throw new Exception("Error diagnostics must include safe stack location without exception messages.");
Console.WriteLine("PASS: internal errors retain stack diagnostics without submitted data.");
sealed class AuditLogger:ILogger<ApiErrors>
{
    public List<string> Entries {get;}=[];
    public IDisposable? BeginScope<TState>(TState state) where TState:notnull => null;
    public bool IsEnabled(LogLevel level)=>true;
    public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter)=>Entries.Add(formatter(state,exception));
}
