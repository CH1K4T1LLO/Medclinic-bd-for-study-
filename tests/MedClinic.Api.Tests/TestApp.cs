using MedClinic.Api.Auth;
using MedClinic.Api.Data;
using MedClinic.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace MedClinic.Api.Tests;

/// <summary>
/// Тестовый хост: InMemory вместо PostgreSQL, детерминированные справочники,
/// выдача JWT нужной роли. Сидер вызывается явно, чтобы не зависеть от порядка старта.
/// </summary>
public class TestApp : IAsyncDisposable
{
    public const string Key = "unit_test_key_that_is_longer_than_32_bytes_!!";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly IServiceScope _scope;
    private readonly MedClinicDbContext _db;

    public TestApp()
    {
        var dbName = $"medclinic-test-{Guid.NewGuid()}";

        // Конфигурация читается из переменных окружения (AppConfig), а не из appsettings.json.
        Environment.SetEnvironmentVariable("MEDCLINIC_JWT_KEY", Key);
        Environment.SetEnvironmentVariable("MEDCLINIC_JWT_ISSUER", "medclinic.api");
        Environment.SetEnvironmentVariable("MEDCLINIC_JWT_AUDIENCE", "medclinic.client");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureServices(services =>
            {
                // Убираем реальный провайдер Npgsql, оставляя только DbContextOptions.
                foreach (var d in services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<MedClinicDbContext>)
                             || d.ServiceType == typeof(DbContextOptions))
                    .ToList())
                {
                    services.Remove(d);
                }

                services.AddDbContext<MedClinicDbContext>(o => o.UseInMemoryDatabase(dbName));
            });
        });

        Client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<MedClinicDbContext>();
        Seed();
    }

    public HttpClient Client { get; }

    public MedClinicDbContext Db => _db;

    public HttpClient As(string role)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return c;
    }

    /// <summary>
    /// Заполняет справочники фиксированными Id. Хостовый сидер тоже выполняется при старте,
    /// поэтому сначала очищаем таблицы, чтобы не поймать дубликаты ключей.
    /// </summary>
    private void Seed()
    {
        _db.ActivityEvents.RemoveRange(_db.ActivityEvents);
        _db.Appointments.RemoveRange(_db.Appointments);
        _db.AuthAudits.RemoveRange(_db.AuthAudits);
        _db.Users.RemoveRange(_db.Users);
        _db.Patients.RemoveRange(_db.Patients);
        _db.Services.RemoveRange(_db.Services);
        _db.Doctors.RemoveRange(_db.Doctors);
        _db.SaveChanges();

        _db.Doctors.AddRange(
            new Doctor { Id = 1, FullName = "Петров П.П.", Specialty = "Терапевт", Department = "Терапия", PopularityWeight = 3.0 },
            new Doctor { Id = 2, FullName = "Сидорова А.М.", Specialty = "Невролог", Department = "Неврология", PopularityWeight = 2.0 },
            new Doctor { Id = 3, FullName = "Иванов И.И.", Specialty = "Хирург", Department = "Хирургия", PopularityWeight = 1.0 });

        _db.Services.AddRange(
            new Service { Id = 1, Name = "Приём терапевта", Price = 2500m, PopularityWeight = 5.0 },
            new Service { Id = 2, Name = "Кардиолог", Price = 3200m, PopularityWeight = 4.0 },
            new Service { Id = 3, Name = "МРТ", Price = 4800m, PopularityWeight = 3.0 });

        _db.Patients.AddRange(
            new Patient { Id = 1, FullName = "Иванов И.И.", Phone = "+79990000001" },
            new Patient { Id = 2, FullName = "Петрова А.А.", Phone = "+79990000002" });

        _db.Users.AddRange(
            new AppUser { Id = 1, Login = "admin", DisplayName = "Администратор", Role = Roles.Admin, PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin-pass") },
            new AppUser { Id = 2, Login = "analyst", DisplayName = "Аналитик", Role = Roles.Analyst, PasswordHash = BCrypt.Net.BCrypt.HashPassword("analyst-pass") },
            new AppUser { Id = 3, Login = "operator", DisplayName = "Оператор", Role = Roles.Operator, PasswordHash = BCrypt.Net.BCrypt.HashPassword("operator-pass") });

        _db.SaveChanges();
    }

    private static string Token(string role)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(Key));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "medclinic.api",
            audience: "medclinic.client",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, role.ToLowerInvariant()),
                new Claim(ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _scope.Dispose();
        await _factory.DisposeAsync();

        Environment.SetEnvironmentVariable("MEDCLINIC_JWT_KEY", null);
    }
}