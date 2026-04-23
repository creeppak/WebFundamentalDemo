namespace Api.Auth;

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);