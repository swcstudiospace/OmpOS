using Autonogrammer.Native.Settings;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>The Tab page's status from the completion configuration.</summary>
public sealed class TabSettingsSourceTests : IDisposable
{
    private readonly HeadTempDirectory _directory = new();

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();

    /// <summary>With nothing configured completion is off, no host is reported and the sentence names the variable that turns it on.</summary>
    [Fact]
    public async Task NothingConfiguredIsOff()
    {
        var source = new TabSettingsSource(static _ => null);

        var values = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.Enabled.ShouldBeFalse();
        values.EndpointHost.ShouldBeNull();
        values.Source.ShouldContain("AUTONOGRAMMER_COMPLETION_ENDPOINT", Case.Sensitive);
    }

    /// <summary>An endpoint variable turns completion on and only its host and port are reported, never the path, query or credentials.</summary>
    [Fact]
    public async Task AnEndpointShowsOnlyHostAndPort()
    {
        var source = new TabSettingsSource(static name =>
            string.Equals(name, "AUTONOGRAMMER_COMPLETION_ENDPOINT", StringComparison.Ordinal) ? "https://user:pw@complete.example:8443/v1/complete?key=abc" : null);

        var values = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.Enabled.ShouldBeTrue();
        values.EndpointHost.ShouldBe("complete.example:8443");
        values.Source.ShouldContain("AUTONOGRAMMER_COMPLETION_ENDPOINT", Case.Sensitive);
    }

    /// <summary>The scheme's default port is left out.</summary>
    [Fact]
    public async Task ADefaultPortIsLeftOut()
    {
        var source = new TabSettingsSource(static name => string.Equals(name, "AUTONOGRAMMER_COMPLETION_ENDPOINT", StringComparison.Ordinal) ? "https://complete.example/x" : null);

        var values = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.EndpointHost.ShouldBe("complete.example");
    }

    /// <summary>An endpoint from a configuration file names the file variable as its source.</summary>
    [Fact]
    public async Task AFileConfigurationNamesTheFileVariable()
    {
        var path = _directory.Write("completion.json", "{\"endpoint\":\"http://127.0.0.1:9000/complete\"}");
        var source = new TabSettingsSource(name => string.Equals(name, "AUTONOGRAMMER_COMPLETION_CONFIG", StringComparison.Ordinal) ? path : null);

        var values = await source.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.Enabled.ShouldBeTrue();
        values.EndpointHost.ShouldBe("127.0.0.1:9000");
        values.Source.ShouldContain("AUTONOGRAMMER_COMPLETION_CONFIG", Case.Sensitive);
    }

    /// <summary>A configuration that cannot be used puts the page in its error state and the message never repeats the configured value.</summary>
    [Fact]
    public async Task AnInvalidEndpointIsAnErrorThatDoesNotRepeatTheValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new TabSettingsSource(static name => string.Equals(name, "AUTONOGRAMMER_COMPLETION_ENDPOINT", StringComparison.Ordinal) ? "ftp://secret-host.example/key=abc123" : null);
        var page = new TabPage(source);

        await page.LoadAsync(ct).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldNotContain("secret-host", Case.Insensitive);
        page.LoadError.ShouldNotContain("abc123", Case.Insensitive);
    }
}
