using MedClinic.Api.Auth;
using MedClinic.Api.Data;
using MedClinic.Api.Configuration;
using MedClinic.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

namespace MedClinic.Api.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Регистрирует JWT-аутентификацию и политики Authorize для ролей Admin / Analyst / Operator.
    /// </summary>
    public static IServiceCollection AddMedClinicAuth(
        this IServiceCollection services, IConfiguration config)
    {
        var jwt = new JwtOptions
        {
            Key = AppConfig.JwtKey,
            Issuer = AppConfig.JwtIssuer,
            Audience = AppConfig.JwtAudience,
            ExpiresHours = AppConfig.JwtExpiresHours
        };

        if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
            throw new InvalidOperationException(
                "Ключ подписи JWT (MEDCLINIC_JWT_KEY) должен содержать не менее 32 байт.");

        // Регистрируем конкретный экземпляр: конфигурация берётся из AppConfig,
        // поэтому внешние файлы appsettings.json не нужны.
        services.AddSingleton(jwt);
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false; // в docker-compose и на локальном стенде используется http
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.AnalystAccess,
                p => p.RequireRole(Roles.Admin, Roles.Analyst));

            options.AddPolicy(Policies.OperatorAccess,
                p => p.RequireRole(Roles.Admin, Roles.Operator));

            // Всё, что не помечено [AllowAnonymous], требует действительного токена.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Наполняет БД начальными справочниками и тестовыми учётными записями.
    /// Пароли из конфигурации хешируются BCrypt и в открытом виде не хранятся.
    /// </summary>
    public static async Task SeedTestUsersAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MedClinicDbContext>();

        if (!await db.Users.AnyAsync(ct))
        {
            var users = new[]
            {
                new AppUser
                {
                    Login = "admin", DisplayName = "Администратор", Role = Roles.Admin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedDefaults.Passwords.Admin)
                },
                new AppUser
                {
                    Login = "analyst", DisplayName = "Аналитик", Role = Roles.Analyst,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedDefaults.Passwords.Analyst)
                },
                new AppUser
                {
                    Login = "operator", DisplayName = "Оператор", Role = Roles.Operator,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedDefaults.Passwords.Operator)
                }
            };
            db.Users.AddRange(users);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Doctors.AnyAsync(ct))
        {
            db.Doctors.AddRange(
                new Doctor { FullName = "Петров П.П.", Specialty = "Терапевт", Department = "Терапия", PopularityWeight = 3.0 },
                new Doctor { FullName = "Сидорова А.М.", Specialty = "Невролог", Department = "Неврология", PopularityWeight = 2.0 },
                new Doctor { FullName = "Иванов И.И.", Specialty = "Хирург", Department = "Хирургия", PopularityWeight = 1.0 });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Services.AnyAsync(ct))
        {
            db.Services.AddRange(
                new Service { Name = "Приём терапевта", Price = 2500m, PopularityWeight = 5.0 },
                new Service { Name = "Кардиолог", Price = 3200m, PopularityWeight = 4.0 },
                new Service { Name = "МРТ", Price = 4800m, PopularityWeight = 3.0 },
                new Service { Name = "Хирург", Price = 3000m, PopularityWeight = 1.0 });
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Patients.AnyAsync(ct))
        {
            db.Patients.AddRange(
                new Patient { FullName = "Иванов И.И.", Phone = "+79990000001" },
                new Patient { FullName = "Петрова А.А.", Phone = "+79990000002" },
                new Patient { FullName = "Сидоров В.В.", Phone = "+79990000003" },
                new Patient { FullName = "Козлова М.М.", Phone = "+79990000004" });
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>
/// Тестовые учётные записи. Пароли задаются переменными окружения
/// MEDCLINIC_PWD_ADMIN / MEDCLINIC_PWD_ANALYST / MEDCLINIC_PWD_OPERATOR.
/// </summary>
public static class SeedDefaults
{
    public static class Passwords
    {
        public static string Admin => Configuration.AppConfig.Passwords.Admin;
        public static string Analyst => Configuration.AppConfig.Passwords.Analyst;
        public static string Operator => Configuration.AppConfig.Passwords.Operator;
    }
}