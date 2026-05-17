using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BookFinder.Web;
using BookFinder.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// In dev, ApiBaseUrl is set in wwwroot/appsettings.json.
// In Docker/production, ApiBaseUrl is empty — nginx proxies /api/ from the same origin.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
apiBaseUrl = string.IsNullOrEmpty(apiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseUrl;

builder.Services.AddHttpClient<BookFinderApiClient>(client =>
    client.BaseAddress = new Uri(apiBaseUrl));

await builder.Build().RunAsync();
