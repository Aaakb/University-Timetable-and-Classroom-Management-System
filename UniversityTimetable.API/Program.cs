using System.Net;
using System.Threading.RateLimiting;
using Data_Access_Layer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;
using UniversityTimetable.API;

var builder = WebApplication.CreateBuilder(args);
// Existing services construct their own context. Configure their existing environment
// hook once at startup rather than maintaining a second connection mechanism.
var configuredConnection = builder.Configuration.GetConnectionString("UniversityTimetable");
if (!string.IsNullOrWhiteSpace(configuredConnection))
    Environment.SetEnvironmentVariable(DatabaseConnection.EnvironmentVariableName, configuredConnection);
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "UniversityTimetable.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "UniversityTimetable.Csrf";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<FacultyScheduleEditorService>();
builder.Services.AddScoped<TimetableAlternativesService>();
builder.Services.AddMemoryCache(options => options.SizeLimit = 64);
builder.Services.AddScoped<SchedulePdfExportService>();
builder.Services.AddSingleton<ResourceCatalog>();
builder.Services.AddSingleton<MutationGate>();
var app = builder.Build();
app.UseMiddleware<ApiErrors>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.IsHttps && context.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { title = "HTTPS is required for network access." });
            return;
        }
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c =>
{
    c.Context.Response.Headers.CacheControl = c.File.Name == "service-worker.js" ? "no-cache" : "public,max-age=0,must-revalidate";
}});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next();
});
app.MapControllers();
// Additive preference tables only; use the desktop maintenance path for general
// database setup. Existing academic and timetable records are untouched here.
await using (var preferenceSchemaContext = new AppDbContext())
    await FacultyPreferenceSchema.EnsureAsync(preferenceSchemaContext);
app.Run();

public partial class Program { }
