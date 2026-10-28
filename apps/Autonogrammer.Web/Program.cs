using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Autonogrammer.Web;
using Autonogrammer.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The API origin comes from wwwroot/appsettings.json (Api:BaseUrl). A ?apiBaseUrl= query override is accepted only for loopback hosts (local
// development). No host is hardcoded. The browser holds no credential: sign-in (OD-03) replaces IApiTokenProvider.
static Uri ResolveApiBase(IServiceProvider sp)
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new Uri(ApiBaseUrlResolver.Resolve(configuration[ApiBaseUrlResolver.ConfigKey], navigation.Uri), UriKind.Absolute);
}

static void AddApiServices(IServiceCollection services) =>
    services.AddAutonogrammerWebServices(
        static _ => new HttpClientHandler(),
        ResolveApiBase,
        sp => new WebWorkbenchOptions(IsDemo: false, ApiHost: ResolveApiBase(sp).Authority));

#if AUTONOGRAMMER_DEMO
// A Demo build (-p:AutonogrammerDemo=true) answers every API route in the browser from scripted fixtures, so no API origin exists. appsettings.demo.json
// (loaded because the Demo build sets the application environment to "demo") sets Demo:Enabled. A Demo build whose Demo:Enabled is not true talks
// to the real API like any other build.
if (string.Equals(builder.Configuration["Demo:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
{
    Autonogrammer.Web.Demo.DemoRegistration.AddAutonogrammerWebDemoServices(builder.Services);
}
else
{
    AddApiServices(builder.Services);
}
#else
AddApiServices(builder.Services);
#endif

await builder.Build().RunAsync().ConfigureAwait(false);
