using System.Net;
using System.Net.Http.Json;
using Shared.Auth;

namespace Web.Clients;

public class AuthClient(HttpClient http)
{
    public async Task<(AccessTokenResponse? Token, IEnumerable<string>? Errors)> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", request, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errors = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(cancellationToken: ct);
            return (null, errors);
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken: ct), null);
    }

    public async Task<AccessTokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken: ct);
    }

    // Refresh token is sent automatically via the HttpOnly cookie set by the API.
    public async Task<AccessTokenResponse?> RefreshAsync(CancellationToken ct = default)
    {
        var response = await http.PostAsync("api/auth/refresh", null, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken: ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default) =>
        await http.PostAsync("api/auth/logout", null, ct);
}
