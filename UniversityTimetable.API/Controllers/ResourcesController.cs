using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniversityTimetable.API.Controllers;

[ApiController, Authorize, Route("api/{resource}")]
public sealed class ResourcesController(ResourceCatalog catalog, MutationGate gate) : ControllerBase
{
    private IResource Find(string resource) => catalog.TryGetValue(resource, out var value)
        ? value : throw new ApiFailure(404, "Unknown resource.");

    [HttpGet]
    public async Task<object> List(string resource, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        if (page < 1 || pageSize is < 1 or > 200) throw new ApiFailure(400, "Invalid pagination parameters.");
        var source = await Find(resource).List();
        if (!string.IsNullOrWhiteSpace(q)) source = source.Where(x => JsonSerializer.Serialize(x).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        return new { items = source.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).Select(Versions.Envelope), total = source.Count, page, pageSize };
    }

    [HttpGet("{id}")]
    public async Task<object> Get(string resource, string id)
    {
        var value = await Find(resource).Get(id) ?? throw new ApiFailure(404, "Record not found.");
        Response.Headers.ETag = $"\"{Versions.Of(value)}\"";
        return Versions.Envelope(value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string resource, [FromBody] JsonElement body)
    {
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        var adapter = Find(resource);
        var result = await adapter.Add(body);
        return Created($"/api/{resource}/{adapter.Key(result)}", Versions.Envelope(result));
    }

    [HttpPut("{id}")]
    public async Task<object> Update(string resource, string id, [FromBody] JsonElement body)
    {
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        var adapter = Find(resource);
        var current = await adapter.Get(id) ?? throw new ApiFailure(404, "Record not found.");
        Versions.Require(Request, current);
        return Versions.Envelope(await adapter.Update(id, body));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string resource, string id)
    {
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        var adapter = Find(resource);
        var current = await adapter.Get(id) ?? throw new ApiFailure(404, "Record not found.");
        Versions.Require(Request, current);
        await adapter.Delete(id);
        return NoContent();
    }
}
