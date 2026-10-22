using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Data_Access_Layer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.BusinessLayer;

namespace UniversityTimetable.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AuthService service, IAntiforgery antiforgery, MutationGate gate) : ControllerBase
{
    [HttpGet("csrf")]
    public object Csrf() => new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken };

    [HttpGet("me"), Authorize]
    public async Task<object> Me()
    {
        await using var db = new AppDbContext();
        int id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.ApplicationUsers.AsNoTracking().Where(x => x.ApplicationUserID == id)
            .Select(x => new { id = x.ApplicationUserID, x.UserName, x.FullName }).SingleOrDefaultAsync();
        if (user is null) { await HttpContext.SignOutAsync(); throw new ApiFailure(401, "الحساب غير متاح."); }
        return user;
    }

    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<object> Login(LoginRequest request)
    {
        var result = await service.SignInAsync(request.UserName, request.Password);
        if (!result.Succeeded) throw new ApiFailure(401, result.Message);
        var user = result.User!;
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, user.ApplicationUserID.ToString()),
            new Claim(ClaimTypes.Name, user.UserName)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return new { id = user.ApplicationUserID, user.UserName, user.FullName };
    }

    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        using var lease = await gate.EnterAsync(HttpContext.RequestAborted);
        var result = await service.RegisterAsync(request.FullName, request.UserName, request.Password, request.ConfirmPassword);
        if (!result.Succeeded) throw new ApiFailure(400, result.Message);
        return StatusCode(201, new { message = "تم إنشاء الحساب. يمكنك تسجيل الدخول الآن." });
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(); return NoContent(); }
}

public sealed record LoginRequest([Required, StringLength(50)] string UserName, [Required, StringLength(512)] string Password);
public sealed record RegisterRequest([Required, StringLength(150)] string FullName,
    [Required, StringLength(50, MinimumLength = 3)] string UserName,
    [Required, StringLength(512, MinimumLength = 4)] string Password,
    [Required, StringLength(512)] string ConfirmPassword);
