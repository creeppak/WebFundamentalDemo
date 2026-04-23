using Api.Auth;
using Infrastructure.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<User> userManager, ITokenService tokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized();

        var (token, expiresAt) = tokenService.GenerateAccessToken(user);
        return Ok(new AuthResponse(token, expiresAt));
    }
}