using System.ComponentModel.DataAnnotations;
using Data_Access_Layer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize(Policy = WebPermissions.Administrator), Route("api/faculty-roster")]
public sealed class FacultyRosterController : ControllerBase
{
    [HttpPost("export")]
    public async Task<IActionResult> Export(FacultyRosterRequest input, CancellationToken token)
    {
        await using var db = new AppDbContext();
        var query = db.FacultyMembers.AsNoTracking();
        if (input.Ids is { } ids)
            query = query.Where(member => ids.Contains(member.FacultyMemberID));
        if (!string.IsNullOrWhiteSpace(input.Query))
        {
            var search = input.Query.Trim();
            query = query.Where(member => member.FullName.Contains(search) ||
                (member.AcademicTitle != null && member.AcademicTitle.Contains(search)));
        }

        var rows = await query.OrderBy(member => member.FacultyMemberID)
            .Select(member => new RosterRow(member.FullName, member.AcademicTitle ?? "")).ToListAsync(token);
        string extension = input.Format;
        string title = input.Ids is null ? "قائمة التدريسيين" : "التدريسيون - آخر دفعة مستوردة";
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "." + extension);
        try
        {
            await Task.Run(() => RosterExportService.Export(path, title, "اسم التدريسي", "اللقب العلمي", rows), token);
            string contentType = extension == "pdf" ? "application/pdf" :
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(await System.IO.File.ReadAllBytesAsync(path, token), contentType, $"faculty.{extension}");
        }
        finally { System.IO.File.Delete(path); }
    }
}

public sealed record FacultyRosterRequest([Required, RegularExpression("^(pdf|xlsx)$")] string Format,
    [StringLength(200)] string? Query, [MaxLength(20000)] int[]? Ids);
