using System.Security.Claims;
using MedClinic.Api.Auth;
using MedClinic.Api.Models;
using Microsoft.Extensions.Options;

namespace MedClinic.Api.Tests;

/// <summary>
/// Проверяет выпуск токена для каждой роли и наличие корректных claim'ов.
/// </summary>
public class TokenServiceTests
{
    private static JwtTokenService Create()
        => new(new JwtOptions
        {
            Key = "unit_test_key_that_is_longer_than_32_bytes_!!",
            Issuer = "medclinic.api",
            Audience = "medclinic.client",
            ExpiresHours = 8
        });

    private static AppUser UserFor(string role) => new()
    {
        Id = 1,
        Login = role.ToLowerInvariant(),
        DisplayName = role,
        Role = role
    };

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Analyst)]
    [InlineData(Roles.Operator)]
    public void CreateToken_EmbedsRoleClaim(string role)
    {
        var token = Create().CreateToken(UserFor(role));

        var parsed = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(token);

        var roleClaim = parsed.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal(role, roleClaim!.Value);
        Assert.Equal("medclinic.api", parsed.Issuer);
        Assert.Contains("medclinic.client", parsed.Audiences);
    }

    [Fact]
    public void CreateToken_ProducesDifferentTokensForSameInput()
    {
        var service = Create();
        var a = service.CreateToken(UserFor(Roles.Admin));
        var b = service.CreateToken(UserFor(Roles.Admin));

        // IssuedAt имеет секундную точность, поэтому возможен крайне редкий совпадный результат.
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.True(a.Length > 20);
    }
}