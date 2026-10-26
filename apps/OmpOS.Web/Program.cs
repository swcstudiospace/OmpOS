using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OmpOS.Web;
using OmpOS.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API origin comes from wwwroot/appsettings.json (Api:BaseUrl). A ?apiBaseUrl= query
// override is accepted only for loopback hosts (local development). No host is hardcoded.
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var baseUrl = ApiBaseUrlResolver.Resolve(configuration[ApiBaseUrlResolver.ConfigKey], navigation.Uri);
    return new ApiSessionClient(new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) });
});
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var baseUrl = ApiBaseUrlResolver.Resolve(configuration[ApiBaseUrlResolver.ConfigKey], navigation.Uri);
    return new SseEventStream(new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) });
});

await builder.Build().RunAsync().ConfigureAwait(false);
