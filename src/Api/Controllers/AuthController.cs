using Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshTokenCookie = "refresh_token";

    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.Register)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, ct);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        var auth = await authService.LoginAsync(request.Email, request.Password, ct);
        if (auth is null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        SetRefreshTokenCookie(auth);
        return StatusCode(StatusCodes.Status201Created, new AccessTokenResponse(auth.AccessToken, auth.ExpiresAt));
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var auth = await authService.LoginAsync(request.Email, request.Password, ct);
        if (auth is null)
            return Unauthorized();

        SetRefreshTokenCookie(auth);
        return Ok(new AccessTokenResponse(auth.AccessToken, auth.ExpiresAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies[RefreshTokenCookie];
        if (rawToken is null)
            return Unauthorized();

        var auth = await authService.RefreshAsync(rawToken, ct);
        if (auth is null)
            return Unauthorized();

        SetRefreshTokenCookie(auth);
        return Ok(new AccessTokenResponse(auth.AccessToken, auth.ExpiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawToken = Request.Cookies[RefreshTokenCookie];
        if (rawToken is not null)
            await authService.RevokeAsync(rawToken, ct);

        Response.Cookies.Delete(RefreshTokenCookie, CookieOptions());
        return NoContent();
    }

    private void SetRefreshTokenCookie(AuthResponse auth) =>
        Response.Cookies.Append(RefreshTokenCookie, auth.RefreshToken, CookieOptions(auth.RefreshExpiresAt));

    private static CookieOptions CookieOptions(DateTime? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
    };
}