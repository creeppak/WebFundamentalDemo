using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Auth;

public class AuthService(
    UserManager<User> userManager,
    ITokenService tokenService,
    AppDbContext dbContext) : IAuthService
{
    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
            return null;

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse?> RefreshAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = tokenService.HashToken(rawToken);
        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
            return null;

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return null;

        stored.IsRevoked = true;
        return await IssueTokensAsync(user, ct);  // SaveChangesAsync covers the revocation + new token atomically
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = tokenService.HashToken(rawToken);
        var stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (stored is not null && !stored.IsRevoked)
        {
            stored.IsRevoked = true;
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, tokenHash, refreshExpiresAt) = tokenService.GenerateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow,
        });

        await dbContext.SaveChangesAsync(ct);
        return new AuthResponse(accessToken, rawRefreshToken, expiresAt);
    }
}