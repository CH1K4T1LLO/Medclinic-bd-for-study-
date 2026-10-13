using MedClinic.Api.Auth;
using MedClinic.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

namespace MedClinic.Api.Tests;

/// <summary>
/// Проверка разграничения доступа по ролям Admin / Analyst / Operator.
/// </summary>
public class AuthorizationTests : IAsyncLifetime
{
    private TestApp _app = null!;

    public Task InitializeAsync() { _app = new TestApp(); return Task.CompletedTask; }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Events_WithoutToken_Returns401()
    {
        var response = await _app.Client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Operator, HttpStatusCode.OK)]
    [InlineData(Roles.Admin, HttpStatusCode.OK)]
    [InlineData(Roles.Analyst, HttpStatusCode.Forbidden)]
    public async Task Events_EnforcesOperatorPolicy(string role, HttpStatusCode expected)
    {
        using var client = _app.As(role);
        var response = await client.GetAsync("/api/events");
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Analyst)]
    [InlineData(Roles.Operator)]
    public async Task Me_WithAnyRole_ReturnsRole(string role)
    {
        using var client = _app.As(role);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(role, body);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwt()
    {
        var response = await _app.Client.PostAsync("/api/auth/login",
            Json("""{"login":"admin","password":"admin-pass"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("access_token", body);
        Assert.Contains(Roles.Admin, body);
    }

    [Theory]
    [InlineData("""{"login":"admin","password":"wrong"}""")]
    [InlineData("""{"login":"unknown","password":"whatever"}""")]
    public async Task Login_WithBadCredentials_Returns401(string payload)
    {
        var response = await _app.Client.PostAsync("/api/auth/login", Json(payload));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FailedLogin_IsWrittenToAuditTable()
    {
        var before = await _app.Db.AuthAudits.CountAsync();

        await _app.Client.PostAsync("/api/auth/login",
            Json("""{"login":"operator","password":"nope"}"""));

        var after = await _app.Db.AuthAudits.CountAsync();
        Assert.Equal(before + 1, after);

        var last = await _app.Db.AuthAudits.OrderByDescending(a => a.Id).FirstAsync();
        Assert.False(last.Succeeded);
        Assert.Equal("invalid_credentials", last.Reason);
        Assert.Equal("operator", last.Login);
    }

    private static StringContent Json(string body)
        => new(body, Encoding.UTF8, "application/json");
}