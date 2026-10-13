using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MedClinic.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace MedClinic.Api.Auth;

public interface ITokenService
{
    string CreateToken(AppUser user);
}

public class JwtTokenService(JwtOptions options) : ITokenService
{
    private readonly JwtOptions _options = options;

    public string CreateToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Login),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(_options.ExpiresHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}