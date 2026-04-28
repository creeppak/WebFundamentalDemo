using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Web.Clients;

namespace Web.Auth;

public class AuthorizationMessageHandler(
    AppAuthenticationStateProvider authProvider,
    AuthClient authClient,
    NavigationManager navigation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = authProvider.AccessToken;
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Buffer body so we can replay it on a retry after token refresh.
        byte[]? bodyBytes = null;
        string? contentType = null;
        if (request.Content is not null)
        {
            bodyBytes = await request.Content.ReadAsByteArrayAsync(ct);
            contentType = request.Content.Headers.ContentType?.ToString();
        }

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var refreshed = await authClient.RefreshAsync(ct);
        if (refreshed is null)
        {
            authProvider.NotifyLogout();
            navigation.NavigateTo("/login");
            return response;
        }

        authProvider.NotifyLogin(refreshed);

        using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        if (bodyBytes is not null)
        {
            retry.Content = new ByteArrayContent(bodyBytes);
            if (contentType is not null)
                retry.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

        return await base.SendAsync(retry, ct);
    }
}
