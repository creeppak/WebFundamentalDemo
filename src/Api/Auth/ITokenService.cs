using Infrastructure.Domain;

namespace Api.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
    (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken();
    string HashToken(string rawToken);
}