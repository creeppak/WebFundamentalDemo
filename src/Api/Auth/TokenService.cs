using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Infrastructure.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

public class TokenService(IConfiguration configuration) : ITokenService
{
    private const int ExpirationMinutes = 15;

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(ExpirationMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
