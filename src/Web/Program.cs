using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Web;
using Web.Auth;
using Web.Clients;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

// Singleton so IHttpClientFactory's internal scope gets the same instance as components.
builder.Services.AddSingleton<AppAuthenticationStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<AppAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<AuthorizationMessageHandler>();

// CookieCredentialsHandler sets credentials:include on AuthClient requests so the browser
// sends the HttpOnly refresh-token cookie and accepts Set-Cookie responses. Required in
// both dev (different port = different origin) and prod (app.{domain} vs api.{domain} =
// different origin despite sharing the same eTLD+1).
builder.Services.AddTransient<CookieCredentialsHandler>();
builder.Services
    .AddHttpClient<AuthClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<CookieCredentialsHandler>();

// All other clients attach the Bearer token and auto-refresh on 401.
builder.Services.AddHttpClient<StocksClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<PortfolioClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();

builder.Services.AddMudServices();

var host = builder.Build();

// Attempt silent token refresh on startup so a page reload doesn't force re-login.
// The HttpOnly refresh-token cookie (set by the API) survives the reload; the in-memory
// access token does not. This restores the session before the UI renders.
var authProvider = host.Services.GetRequiredService<AppAuthenticationStateProvider>();
var authClient = host.Services.GetRequiredService<AuthClient>();
await authProvider.TryRestoreSessionAsync(authClient);

await host.RunAsync();
