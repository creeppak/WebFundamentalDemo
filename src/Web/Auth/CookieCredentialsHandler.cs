using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Web.Auth;

// Tells the browser's fetch API to include cookies on cross-origin requests.
// Required because the API runs on a different port in dev, making it cross-origin.
// Without this, Set-Cookie responses are silently dropped and cookies are never sent.
public class CookieCredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
