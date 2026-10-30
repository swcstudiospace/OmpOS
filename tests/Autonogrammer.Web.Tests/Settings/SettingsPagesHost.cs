using Autonogrammer.Presentation.Settings;
using Autonogrammer.Web.Services;
using Autonogrammer.Web.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>The Settings window the web head builds from its registered pages, over the Demo handler of a <see cref="DemoTestHost"/>.</summary>
internal sealed class SettingsPagesHost : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>Initializes a new instance of the <see cref="SettingsPagesHost"/> class.</summary>
    /// <param name="host">The Demo host the pages read from.</param>
    /// <param name="js">The JavaScript runtime the General page uses for the theme.</param>
    internal SettingsPagesHost(DemoTestHost host, IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(js);
        var services = new ServiceCollection();
        services.AddSingleton(_ => new ApiHttp(host.Http));
        services.AddSingleton(new WebWorkbenchOptions(IsDemo: true, ApiHost: "demo (in this browser)"));
        services.AddWebSettingsPages();
        _provider = services.BuildServiceProvider();
        Window = WebSettingsFactory.Create(host.Api, js, "demo (in this browser)", host.Time, _provider.GetServices<ISettingsPage>());
    }

    /// <summary>Gets the window model with every registered page.</summary>
    internal SettingsWindowModel Window { get; }

    /// <inheritdoc />
    public void Dispose() => _provider.Dispose();
}
