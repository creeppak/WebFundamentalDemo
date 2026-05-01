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

// AuthClient has no handler — it IS the auth client (login, refresh, logout).
builder.Services.AddHttpClient<AuthClient>(c => c.BaseAddress = new Uri(apiBaseUrl));

// All other clients attach the Bearer token and auto-refresh on 401.
builder.Services.AddHttpClient<StocksClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<PortfolioClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();

builder.Services.AddMudServices();

await builder.Build().RunAsync();
