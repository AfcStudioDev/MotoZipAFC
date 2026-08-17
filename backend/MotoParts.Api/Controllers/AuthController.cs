using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotoParts.Infrastructure.Persistence;
using MotoParts.Application.Contracts;
using MotoParts.Domain.Models;
using MotoParts.Application.Warehouse;
using MotoParts.Infrastructure.Services;

namespace MotoParts.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    TokenService tokenService,
    OAuthService oauthService,
    IEmailService emailService,
    IConfiguration config) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email и пароль обязательны" });
        if (request.Password.Length < 6)
            return BadRequest(new { message = "Пароль должен быть не короче 6 символов" });
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Пользователь с таким email уже существует" });

        var user = new User
        {
            Email = email,
            FIO = request.FIO.Trim(),
            PhoneNumber = request.PhoneNumber,
            PasswordHash = PasswordHasher.Hash(request.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(ToAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user?.PasswordHash is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Неверный email или пароль" });

        return Ok(ToAuthResponse(user));
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleLoginRequest request)
    {
        var info = await oauthService.VerifyGoogleAsync(request.IdToken);
        if (info is null)
            return Unauthorized(new { message = "Не удалось проверить токен Google" });

        var user = await FindOrCreateOAuthUserAsync("google", info);
        return Ok(ToAuthResponse(user));
    }

    [HttpPost("vk")]
    public async Task<ActionResult<AuthResponse>> Vk(VkLoginRequest request)
    {
        var info = await oauthService.VerifyVkAsync(request.Code, request.RedirectUri);
        if (info is null)
            return Unauthorized(new { message = "Не удалось авторизоваться через VK" });

        var user = await FindOrCreateOAuthUserAsync("vk", info);
        return Ok(ToAuthResponse(user));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Ответ одинаковый вне зависимости от того, найден ли пользователь,
        // чтобы нельзя было перебором выяснить зарегистрированные email.
        if (user is not null)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.PasswordResetTokenHash = Sha256(token);
            user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
            await db.SaveChangesAsync();

            var frontendUrl = config["Frontend:BaseUrl"] ?? "http://192.168.88.122:4200";
            var link = $"{frontendUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={token}";
            await emailService.SendPasswordResetLinkAsync(
                email,
                $"{link}");
        }

        return Ok(new { message = "Если такой email зарегистрирован, на него отправлена ссылка для сброса пароля" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword.Length < 6)
            return BadRequest(new { message = "Пароль должен быть не короче 6 символов" });

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user?.PasswordResetTokenHash is null ||
            user.PasswordResetTokenExpiresAt < DateTimeOffset.UtcNow ||
            user.PasswordResetTokenHash != Sha256(request.Token))
        {
            return BadRequest(new { message = "Ссылка для сброса пароля недействительна или устарела" });
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        await db.SaveChangesAsync();

        return Ok(new { message = "Пароль изменён" });
    }

    private async Task<User> FindOrCreateOAuthUserAsync(string provider, ExternalUserInfo info)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            u => (u.OAuthProvider == provider && u.OAuthSubject == info.Subject) || u.Email == info.Email.ToLower());
        if (user is null)
        {
            user = new User
            {
                Email = info.Email.ToLowerInvariant(),
                FIO = info.Name,
                OAuthProvider = provider,
                OAuthSubject = info.Subject,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else if (user.OAuthProvider is null)
        {
            // привязываем OAuth к существующему аккаунту с тем же email
            user.OAuthProvider = provider;
            user.OAuthSubject = info.Subject;
            await db.SaveChangesAsync();
        }

        return user;
    }

    private AuthResponse ToAuthResponse(User user) => new(
        tokenService.CreateToken(user),
        new UserDto(user.Id, user.Email, user.FIO, user.PhoneNumber, user.IsAdmin, user.IsRegistrar, user.IsSender));

    private static string Sha256(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
