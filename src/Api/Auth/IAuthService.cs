namespace Api.Auth;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct);
    Task<AuthResponse?> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}