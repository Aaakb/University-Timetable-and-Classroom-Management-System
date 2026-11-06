using System.Security.Claims;
namespace UniversityTimetable.API;

// Existing accounts retain their identity; privileges are assigned by the host, not by registration.
public static class WebPermissions
{
    public const string Administrator = "Administrator";
    public static bool IsAdministrator(IConfiguration configuration, int userId) =>
        (configuration.GetSection("Authorization:AdministratorIds").Get<int[]>() ?? []).Contains(userId);
    public static void RequireStrongPassword(string password)
    {
        if (password.Length < 12) throw new ApiFailure(400, "كلمة المرور الجديدة يجب أن تتكون من 12 حرفاً على الأقل.");
    }
}
