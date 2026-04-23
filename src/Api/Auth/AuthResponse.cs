namespace Api.Auth;

public record AuthResponse(string AccessToken, DateTime ExpiresAt);