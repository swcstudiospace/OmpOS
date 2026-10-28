using System.Net;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;
using OmpOS.Web.Services;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The Settings window of the web head: which pages it offers, what each source reads, and how an extra page replaces one.</summary>
public sealed class WebSettingsTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    /// <summary>Only the sections the browser or the API can serve are offered, in section order.</summary>
    [Fact]
    public void WindowOffersTheSectionsTheWebHeadCanServe()
    {
        var window = WebSettingsFactory.Create(_host.Api, new NoJsRuntime(), "api.example", TimeProvider.System, []);

        window.Pages.Select(static page => page.Section).ShouldBe(new[]
        {
            SettingsSection.General,
            SettingsSection.Tab,
            SettingsSection.Plugins,
            SettingsSection.Network,
            SettingsSection.Keyboard,
        });
    }

    /// <summary>A page another service registers for a section replaces the built-in page, and adds sections the web head has no page for.</summary>
    [Fact]
    public void ExtraPagesReplaceAndExtendTheWindow()
    {
        var replacement = new FakePage(SettingsSection.Plugins);
        var added = new FakePage(SettingsSection.Models);

        var window = WebSettingsFactory.Create(_host.Api, new NoJsRuntime(), "api.example", TimeProvider.System, [replacement, added]);

        window.Pages.Select(static page => page.Section).ShouldBe(new[]
        {
            SettingsSection.Models,
            SettingsSection.General,
            SettingsSection.Tab,
            SettingsSection.Plugins,
            SettingsSection.Network,
            SettingsSection.Keyboard,
        });
        window.Pages.Single(static page => page.Section == SettingsSection.Plugins).ShouldBeSameAs(replacement);
    }

    /// <summary>The Plugins page shows the workspace plugin check; the Tab page says tab completion is a desktop feature; the Keyboard page lists the fixed shortcuts.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task PagesLoadTheirDataFromTheApiAndTheHead()
    {
        var ct = TestContext.Current.CancellationToken;
        var plugins = new PluginsPage(new WebPluginsSettingsSource(_host.Api));
        var tab = new TabPage(new WebTabSettingsSource());
        var keyboard = new KeyboardSettingsPage(new WebKeyboardSettingsSource());

        await plugins.LoadAsync(ct).ConfigureAwait(true);
        await tab.LoadAsync(ct).ConfigureAwait(true);
        await keyboard.LoadAsync(ct).ConfigureAwait(true);

        plugins.State.ShouldBe(SettingsPageState.Ready);
        plugins.Rows.Single().IsFound.ShouldBeTrue();
        plugins.Rows.Single().Detail.ShouldBe("Ultrathink, Swarm, Substrate and GSD-Core are connected (Demo).");
        tab.State.ShouldBe(SettingsPageState.Ready);
        tab.IsEnabled.ShouldBeFalse();
        keyboard.State.ShouldBe(SettingsPageState.Ready);
    }

    /// <summary>An unreachable API leaves the Plugins page in its Error state with the reason, not an exception.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task PluginsPageShowsAnErrorWhenTheApiIsUnreachable()
    {
        using var handler = new RoutedHandler();
        handler.On(HttpMethod.Get, "/workspace", _ => throw new HttpRequestException("offline"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        var page = new PluginsPage(new WebPluginsSettingsSource(new OmpApiClient(http)));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe(WebFailureText.Unreachable);
    }

    /// <summary>The network check calls the API readiness route and reports reachable, not ready, or unreachable.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task NetworkCheckReportsTheApiReadiness()
    {
        var ct = TestContext.Current.CancellationToken;
        using var ready = new RoutedHandler();
        ready.On(HttpMethod.Get, "/health/ready", _ => new HttpResponseMessage(HttpStatusCode.OK));
        using var notReady = new RoutedHandler();
        notReady.On(HttpMethod.Get, "/health/ready", _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var offline = new RoutedHandler();
        offline.On(HttpMethod.Get, "/health/ready", _ => throw new HttpRequestException("offline"));

        var good = await CheckAsync(ready, ct).ConfigureAwait(true);
        var bad = await CheckAsync(notReady, ct).ConfigureAwait(true);
        var down = await CheckAsync(offline, ct).ConfigureAwait(true);

        good.Status.ShouldBe(EndpointStatus.Reachable);
        good.Host.ShouldBe("api.example");
        bad.Status.ShouldBe(EndpointStatus.Unreachable);
        down.Status.ShouldBe(EndpointStatus.Unreachable);
        down.Message.ShouldBe(WebFailureText.Unreachable);
    }

    /// <summary>Stored theme and motion keys map to the preferences; an unknown key falls back to the page default.</summary>
    /// <param name="key">The stored key.</param>
    /// <param name="theme">The expected theme.</param>
    [Theory]
    [InlineData("system", ThemePreference.System)]
    [InlineData("dark", ThemePreference.Dark)]
    [InlineData("light", ThemePreference.Light)]
    [InlineData("nonsense", ThemePreference.Light)]
    [InlineData(null, ThemePreference.Light)]
    public void StoredThemeKeysMapToPreferences(string? key, ThemePreference theme) =>
        WebGeneralSettingsSource.ThemeOf(key).ShouldBe(theme);

    /// <summary>Stored motion keys map to the preferences.</summary>
    [Fact]
    public void StoredMotionKeysMapToPreferences()
    {
        WebGeneralSettingsSource.MotionOf("reduce").ShouldBe(MotionPreference.Reduced);
        WebGeneralSettingsSource.MotionOf("full").ShouldBe(MotionPreference.Full);
        WebGeneralSettingsSource.MotionOf("system").ShouldBe(MotionPreference.System);
        WebGeneralSettingsSource.MotionOf("x").ShouldBe(MotionPreference.System);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private static async Task<EndpointCheck> CheckAsync(RoutedHandler handler, CancellationToken cancellationToken)
    {
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        var report = await new WebNetworkDiagnosticsSource(new OmpApiClient(http), "api.example", TimeProvider.System).RunAsync(cancellationToken).ConfigureAwait(true);
        return report.Checks.Single();
    }

    private sealed class FakePage(SettingsSection section) : ISettingsPage
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public SettingsSection Section { get; } = section;

        public string Title => Section.ToString();

        public bool IsDirty => false;

        public bool HasError => false;

        public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> SaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public void Reset()
        {
        }

        public void Discard()
        {
        }
    }
}
