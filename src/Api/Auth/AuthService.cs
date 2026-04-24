using Infrastructure.Data;
using Infrastructure.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Auth;

public class AuthService(
    UserManager<User> userManager,
    ITokenService tokenService,
    AppDbContext dbContext,
    IOptions<RegistrationOptions> registrationOptions) : IAuthService
{
    private readonly RegistrationOptions _registrationOptions = registrationOptions.Value;

    public async Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken ct)
    {
        var user = new User { UserName = email, Email = email };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var identityResult = await userManager.CreateAsync(user, password);
            if (!identityResult.Succeeded)
                return new RegisterResult(false, identityResult.Errors.Select(e => e.Description));

            dbContext.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TransactionType = TransactionType.Deposit,
                Price = 0m,
                Quantity = _registrationOptions.InitialDepositAmount,
            });

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new RegisterResult(true, []);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

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
        return new AuthResponse(accessToken, rawRefreshToken, expiresAt, refreshExpiresAt);
    }
}