using MedClinic.Api.Auth;
using MedClinic.Api.Configuration;
using MedClinic.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

// Конфигурация целиком в коде: внешние appsettings.json не используются,
// значения переопределяются переменными окружения (см. AppConfig).
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://localhost:{AppConfig.Port}");

builder.Services.AddDbContext<MedClinicDbContext>(o =>
    o.UseNpgsql(AppConfig.ConnectionString));

builder.Services.AddMedClinicAuth(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MedClinic.Analytics API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите JWT: кнопка Authorize в Swagger UI"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddScoped<DatabaseHealthCheck>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Логирование отказов авторизации: 401 и 403 фиксируются как Warning.
app.Use(async (ctx, next) =>
{
    await next();
    var code = ctx.Response.StatusCode;
    if (code is 401 or 403)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth");
        logger.LogWarning("auth.denied {Method} {Path} -> {Status} role={Role}",
            ctx.Request.Method, ctx.Request.Path, code,
            ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "anonymous");
    }
});

if (AppConfig.SwaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Схема применяется миграциями EF Core, затем наполняются справочники и тестовые учётные записи.
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MedClinicDbContext>();

    // InMemory используется только в тестах: миграции для него неприменимы.
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
}
await app.Services.SeedTestUsersAsync();

app.Run();

/// <summary>Точка входа помечена как public, чтобы интеграционные тесты (WebApplicationFactory) могли её использовать.</summary>
public partial class Program;

/// <summary>Health-check доступности PostgreSQL для эндпоинта /health.</summary>
internal class DatabaseHealthCheck(MedClinicDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext ctx, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Не удалось подключиться к базе данных");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}