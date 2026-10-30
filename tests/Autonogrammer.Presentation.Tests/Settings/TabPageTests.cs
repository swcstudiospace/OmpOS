using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Tab page: a read-only status of inline completion that never shows a full URL or credentials.</summary>
public sealed class TabPageTests
{
    /// <summary>With no endpoint the page says completion is off and how it is turned on; nothing is editable or dirty.</summary>
    [Fact]
    public async Task OffShowsHowItIsConfigured()
    {
        var page = new TabPage(new ScriptedTabSource());

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsEnabled.ShouldBeFalse();
        page.StatusText.ShouldBe(SettingsCopy.TabStatusOff);
        page.EndpointText.ShouldBe(SettingsCopy.TabEndpointNone);
        page.HowConfiguredText.ShouldContain("AUTONOGRAMMER_COMPLETION_ENDPOINT", Case.Sensitive);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>With an endpoint the page shows only its host and port.</summary>
    [Fact]
    public async Task OnShowsHostAndPort()
    {
        var page = new TabPage(new ScriptedTabSource { Values = new TabSettingsValues(true, "localhost:11434", "AUTONOGRAMMER_COMPLETION_ENDPOINT") });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.IsEnabled.ShouldBeTrue();
        page.StatusText.ShouldBe(SettingsCopy.TabStatusOn);
        page.EndpointText.ShouldBe("localhost:11434");
        page.HowConfiguredText.ShouldContain("AUTONOGRAMMER_COMPLETION_ENDPOINT", Case.Sensitive);
    }

    /// <summary>Whatever a source hands over, the page shows at most a host and a port, never a scheme, credentials, path, query or fragment.</summary>
    [Theory]
    [InlineData("https://user:s3cret@api.example.com:8443/v1/complete?key=abc123#frag", "api.example.com:8443")]
    [InlineData("http://localhost:11434/v1", "localhost:11434")]
    [InlineData("user:s3cret@api.example.com", "api.example.com")]
    [InlineData("https://[::1]:8080/x", "[::1]:8080")]
    [InlineData("api.example.com", "api.example.com")]
    public async Task NeverShowsMoreThanHostAndPort(string reported, string expected)
    {
        var page = new TabPage(new ScriptedTabSource { Values = new TabSettingsValues(true, reported, "AUTONOGRAMMER_COMPLETION_ENDPOINT") });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.EndpointText.ShouldBe(expected);
        page.EndpointText.ShouldNotContain("s3cret", Case.Sensitive);
        page.EndpointText.ShouldNotContain("abc123", Case.Sensitive);
        page.EndpointText.ShouldNotContain("/", Case.Sensitive);
    }

    /// <summary>An endpoint that cannot be reduced to a safe host is hidden, not shown raw.</summary>
    [Theory]
    [InlineData("not a host")]
    [InlineData("user:pa/ss@host.example")]
    [InlineData("host.example:port")]
    [InlineData("host.example:123456")]
    [InlineData("https://")]
    [InlineData("")]
    public async Task AnEndpointThatCannotBeShownSafelyIsHidden(string reported)
    {
        var page = new TabPage(new ScriptedTabSource { Values = new TabSettingsValues(true, reported, "AUTONOGRAMMER_COMPLETION_ENDPOINT") });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.EndpointText.ShouldBe(SettingsCopy.TabEndpointHidden);
    }

    /// <summary>A load failure becomes the error state with the source's reason.</summary>
    [Fact]
    public async Task ALoadFailureBecomesTheErrorState()
    {
        var source = new ScriptedTabSource { LoadFailure = "completion settings are unreadable" };
        var page = new TabPage(source);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("completion settings are unreadable");

        source.LoadFailure = null;
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.LoadError.ShouldBeEmpty();
    }

    /// <summary>The page is read-only: never dirty, saving succeeds without writing, reset and discard change nothing, and its source has no write.</summary>
    [Fact]
    public async Task ThePageIsReadOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var page = new TabPage(new ScriptedTabSource { Values = new TabSettingsValues(true, "localhost:11434", "AUTONOGRAMMER_COMPLETION_ENDPOINT") });
        await page.LoadAsync(token).ConfigureAwait(true);

        page.Reset();
        page.Discard();

        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
        page.EndpointText.ShouldBe("localhost:11434");
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        typeof(ITabSettingsSource).GetMethods().Select(method => method.Name).ToArray().ShouldBe(["LoadAsync"]);
    }

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new TabPage(null!));
}
