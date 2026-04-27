namespace Shared.Auth;

public record AccessTokenResponse(string AccessToken, DateTime ExpiresAt);
