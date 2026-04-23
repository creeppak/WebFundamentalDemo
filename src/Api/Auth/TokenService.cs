using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Infrastructure.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

public class TokenService(IConfiguration configuration) : ITokenService
{
    private const int AccessTokenExpirationMinutes = 15;
    private const int RefreshTokenExpirationDays = 7;

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes);

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

    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (rawToken, HashToken(rawToken), DateTime.UtcNow.AddDays(RefreshTokenExpirationDays));
    }

    public string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hash);
    }
}
