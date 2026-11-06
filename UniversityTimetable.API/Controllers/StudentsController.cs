using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize(Policy = WebPermissions.Administrator), Route("api/students")]
public sealed class StudentsController(MutationGate gate) : ControllerBase
{
    private readonly StudentService students = new();

    [HttpGet("{stage:int}")]
    public async Task<object> List(int stage) => new
    {
        students = await students.ListAsync(stage),
        groups = await students.GroupsAsync(stage)
    };

    [HttpGet("{stage:int}/groups")]
    public async Task<object> Groups(int stage) => await students.GroupsAsync(stage);

    [HttpPost("{stage:int}")]
    public async Task<object> Add(int stage, StudentInput input, CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        return await students.AddAsync(input.FullName, stage, input.SectionId);
    }

    [HttpPost("{stage:int}/import"), RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<object> Import(int stage, [FromForm] IFormFile file, [FromForm] int? sectionId,
        CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        return await ListImportsController.ReadUpload(file, [".xlsx", ".csv"], token, async path => await students.ImportWithDetailsAsync(path, stage, sectionId));
    }

    [HttpPut("{studentId:int}/name")]
    public async Task<IActionResult> Rename(int studentId, StudentNameInput input, CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        await students.RenameAsync(studentId, input.FullName);
        return NoContent();
    }

    [HttpPost("{stage:int}/change-section")]
    public async Task<IActionResult> ChangeSection(int stage, StudentSectionChange input, CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        await students.ChangeSectionAsync(stage, input.StudentId, input.SourceId, input.DestinationId);
        return NoContent();
    }

    [HttpPost("{stage:int}/split")]
    public async Task<IActionResult> Split(int stage, StudentSectionInput input, CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        await students.SplitSectionAsync(stage, input.SectionId);
        return NoContent();
    }

    [HttpGet("{stage:int}/export")]
    public async Task<IActionResult> Export(int stage, string format, int? sectionId, CancellationToken token)
    {
        if (format is not ("pdf" or "xlsx")) throw new ArgumentException("اختر PDF أو Excel.");
        var groups = (await students.GroupsAsync(stage)).ToDictionary(group => group.Id, group => group.Label);
        if (sectionId is not null and not -1 && !groups.ContainsKey(sectionId.Value))
            throw new ArgumentException("اختر شعبة أو تخصصًا صالحًا.");
        var roster = (await students.ListAsync(stage))
            .Where(row => sectionId is null || row.SectionID == (sectionId == -1 ? null : sectionId))
            .OrderBy(row => row.PracticalGroupNumber ?? 3)
            .Select(row =>
            {
                string label = row.SectionID is int id && groups.TryGetValue(id, out var group) ? group : "غير موزّع";
                if (row.PracticalGroupNumber is int number) label += $" · المجموعة {number}";
                return new RosterRow(row.FullName, label);
            }).ToArray();
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "." + format);
        try
        {
            await Task.Run(() => RosterExportService.Export(path, $"طلبة المرحلة {stage}", "اسم الطالب", "الشعبة أو التخصص", roster), token);
            string contentType = format == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return File(await System.IO.File.ReadAllBytesAsync(path, token), contentType, $"students-{stage}.{format}");
        }
        finally { System.IO.File.Delete(path); }
    }

    [HttpPost("{stage:int}/move")]
    public async Task<object> Move(int stage, StudentMove input, CancellationToken token)
    {
        using var lease = await gate.EnterAsync(token);
        var count = input.Promote
            ? await students.PromoteAsync(stage, input.StudentIds, input.SectionId)
            : await students.AssignGroupAsync(stage, input.StudentIds,
                input.SectionId ?? throw new ArgumentException("اختر الشعبة أو التخصص."));
        return new { count };
    }
}

public sealed record StudentInput([Required, StringLength(150)] string FullName, int? SectionId);
public sealed record StudentMove([Required, MinLength(1), MaxLength(2000)] int[] StudentIds, int? SectionId, bool Promote);

public sealed record StudentNameInput([Required, StringLength(150)] string FullName);
public sealed record StudentSectionInput([Range(1, int.MaxValue)] int SectionId);
public sealed record StudentSectionChange([Range(1, int.MaxValue)] int StudentId,
    [Range(1, int.MaxValue)] int SourceId, [Range(1, int.MaxValue)] int DestinationId);
