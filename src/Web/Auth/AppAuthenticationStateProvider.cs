using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Shared.Auth;
using Web.Clients;

namespace Web.Auth;

public class AppAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal());

    private string? _accessToken;
    private DateTime _expiresAt;

    public string? AccessToken => _accessToken != null && DateTime.UtcNow < _expiresAt
        ? _accessToken
        : null;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = AccessToken;
        if (token is null)
            return Task.FromResult(Anonymous);

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public async Task TryRestoreSessionAsync(AuthClient authClient)
    {
        var response = await authClient.RefreshAsync();
        if (response is not null)
            NotifyLogin(response);
    }

    public void NotifyLogin(AccessTokenResponse response)
    {
        _accessToken = response.AccessToken;
        _expiresAt = response.ExpiresAt;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyLogout()
    {
        _accessToken = null;
        _expiresAt = default;
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var segments = jwt.Split('.');
        if (segments.Length != 3)
            return [];

        var payload = Base64UrlDecode(segments[1]);
        var json = Encoding.UTF8.GetString(payload);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (dict is null)
            return [];

        var claims = new List<Claim>();
        foreach (var (key, value) in dict)
        {
            var claimType = key switch
            {
                "sub"   => ClaimTypes.NameIdentifier,
                "email" => ClaimTypes.Email,
                "name"  => ClaimTypes.Name,
                _       => key
            };

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in value.EnumerateArray())
                    claims.Add(new Claim(claimType, element.GetString() ?? string.Empty));
            }
            else
            {
                claims.Add(new Claim(claimType, value.ToString()));
            }
        }

        return claims;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = (input.Length % 4) switch
        {
            2 => input + "==",
            3 => input + "=",
            _ => input
        };
        return Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
    }
}
