using Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Auth;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshTokenCookie = "refresh_token";

    /// <summary>Register a new user account and return an access token.</summary>
    /// <response code="201">Registration successful. Returns a short-lived access token; refresh token is set as an HttpOnly cookie.</response>
    /// <response code="400">Validation failed (e.g. email already in use, weak password).</response>
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.Register)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>Log in with email and password and return an access token.</summary>
    /// <response code="200">Login successful. Returns a short-lived access token; refresh token is set as an HttpOnly cookie.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var auth = await authService.LoginAsync(request.Email, request.Password, ct);
        if (auth is null)
            return Unauthorized();

        SetRefreshTokenCookie(auth);
        return Ok(new AccessTokenResponse(auth.AccessToken, auth.ExpiresAt));
    }

    /// <summary>Exchange a valid refresh token (HttpOnly cookie) for a new access token.</summary>
    /// <response code="200">Returns a new access token; refresh token cookie is rotated.</response>
    /// <response code="401">Refresh token missing, expired, or already revoked.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Revoke the current refresh token and clear the auth cookie.</summary>
    /// <response code="204">Logged out successfully.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
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
