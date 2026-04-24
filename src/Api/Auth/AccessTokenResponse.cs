namespace Api.Auth;

public record AccessTokenResponse(string AccessToken, DateTime ExpiresAt);
