using System.Security.Claims;
using MedClinic.Api.Auth;
using MedClinic.Api.Data;
using MedClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedClinic.Api.Controllers;

public record LoginRequest(string Login, string Password);

/// <summary>Ответ на успешный вход. Имена полей в snake_case — как в документации API.</summary>
public record LoginResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
    [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
    [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresInSeconds,
    [property: System.Text.Json.Serialization.JsonPropertyName("login")] string Login,
    [property: System.Text.Json.Serialization.JsonPropertyName("role")] string Role);

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(
    MedClinicDbContext db,
    ITokenService tokens,
    ILogger<AuthController> log) : ControllerBase
{
    /// <summary>Вход по логину и паролю. Возвращает JWT.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        ua = ua.Length > 256 ? ua[..256] : ua;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == request.Login, ct);

        // Единый текст ошибки, чтобы не давать перебор понять, существует ли логин.
        if (user is null)
        {
            await WriteAuditAsync(request.Login, false, "unknown_user", ip, ua, ct);
            log.LogWarning("Auth failed for {Login} from {Ip}: unknown_user", request.Login, ip);
            return Unauthorized(new { error = "invalid_credentials" });
        }

        if (!user.IsActive)
        {
            await WriteAuditAsync(request.Login, false, "user_inactive", ip, ua, ct);
            log.LogWarning("Auth failed for {Login} from {Ip}: user_inactive", request.Login, ip);
            return Unauthorized(new { error = "user_inactive" });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await WriteAuditAsync(request.Login, false, "invalid_credentials", ip, ua, ct);
            log.LogWarning("Auth failed for {Login} from {Ip}: invalid_credentials", request.Login, ip);
            return Unauthorized(new { error = "invalid_credentials" });
        }

        await WriteAuditAsync(request.Login, true, null, ip, ua, ct);
        log.LogInformation("Auth succeeded for {Login} ({Role}) from {Ip}", user.Login, user.Role, ip);

        return Ok(new LoginResponse(
            tokens.CreateToken(user),
            "Bearer",
            8 * 3600,
            user.Login,
            user.Role));
    }

    /// <summary>Кто я — по текущему токену.</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "unknown";
        return Ok(new
        {
            login = User.Identity?.Name,
            userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            role,
            correlationId = HttpContext.TraceIdentifier
        });
    }

    /// <summary>
    /// Записывает попытку входа в аудит. Пароль и сам токен в журнал не попадают.
    /// </summary>
    private async Task WriteAuditAsync(string login, bool ok, string? reason, string? ip, string ua, CancellationToken ct)
    {
        db.AuthAudits.Add(new AuthAudit
        {
            Login = login,
            Succeeded = ok,
            Reason = reason,
            IpAddress = ip,
            UserAgent = ua
        });
        await db.SaveChangesAsync(ct);
    }
}