using System.Security.Cryptography;
using Data_Access_Layer;
using Microsoft.EntityFrameworkCore;
using University_Timetable_and_Classroom_Management_System.Models;

namespace University_Timetable_and_Classroom_Management_System.BusinessLayer
{
    public class AuthService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;
        private const int MinimumPasswordLength = 4;

        public async Task<AuthResult> SignInAsync(string userName, string password)
        {
            string normalizedUserName = NormalizeUserName(userName);

            if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
            {
                return AuthResult.Failed("Enter the user name and password.");
            }

            await using var context = new AppDbContext();
            var user = await context.ApplicationUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.NormalizedUserName == normalizedUserName);

            if (user is null || !VerifyPassword(password, user.PasswordSalt, user.PasswordHash))
            {
                return AuthResult.Failed("Invalid user name or password.");
            }

            return AuthResult.Success(user, "Signed in successfully.");
        }

        public async Task<AuthResult> RegisterAsync(
            string fullName,
            string userName,
            string password,
            string confirmPassword)
        {
            fullName = (fullName ?? string.Empty).Trim();
            userName = (userName ?? string.Empty).Trim();
            string normalizedUserName = NormalizeUserName(userName);

            string? validationMessage = ValidateRegistration(fullName, userName, password, confirmPassword);

            if (validationMessage is not null)
            {
                return AuthResult.Failed(validationMessage);
            }

            await using var context = new AppDbContext();
            bool exists = await context.ApplicationUsers
                .AnyAsync(item => item.NormalizedUserName == normalizedUserName);

            if (exists)
            {
                return AuthResult.Failed("This user name is already used.");
            }

            var user = new ApplicationUser
            {
                ApplicationUserID = await AutoKeyGenerator.FirstAvailableAsync(
                    context.ApplicationUsers.Select(item => item.ApplicationUserID)),
                FullName = fullName,
                UserName = userName,
                NormalizedUserName = normalizedUserName,
                CreatedAtUtc = DateTime.UtcNow
            };

            (user.PasswordSalt, user.PasswordHash) = HashPassword(password);

            await context.ApplicationUsers.AddAsync(user);
            await ManualKeySaveHelper.SaveWithManualKeyAsync(context, "[ApplicationUsers]");

            return AuthResult.Success(user, "Account created successfully.");
        }

        private static string? ValidateRegistration(
            string fullName,
            string userName,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return "Enter the full name.";
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                return "Enter the user name.";
            }

            if (userName.Length < 3)
            {
                return "User name must be at least 3 characters.";
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength)
            {
                return $"Password must be at least {MinimumPasswordLength} characters.";
            }

            if (password != confirmPassword)
            {
                return "Password confirmation does not match.";
            }

            return null;
        }

        private static string NormalizeUserName(string userName)
        {
            return (userName ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static (string Salt, string Hash) HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        private static bool VerifyPassword(string password, string saltText, string hashText)
        {
            byte[] salt = Convert.FromBase64String(saltText);
            byte[] expectedHash = Convert.FromBase64String(hashText);
            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }

    public sealed record AuthResult(bool Succeeded, string Message, ApplicationUser? User)
    {
        public static AuthResult Success(ApplicationUser user, string message)
        {
            return new AuthResult(true, message, user);
        }

        public static AuthResult Failed(string message)
        {
            return new AuthResult(false, message, null);
        }
    }
}
