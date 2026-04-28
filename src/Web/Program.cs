using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Web;
using Web.Clients;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

builder.Services.AddHttpClient<AuthClient>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<StocksClient>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<PortfolioClient>(c => c.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddMudServices();

await builder.Build().RunAsync();
