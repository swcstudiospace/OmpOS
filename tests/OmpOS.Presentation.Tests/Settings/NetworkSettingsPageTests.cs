using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>The Network page: diagnostics run only on request, one run at a time, cancellable, and a run never throws out of the page.</summary>
public sealed class NetworkSettingsPageTests
{
    private static async Task<NetworkSettingsPage> ReadyPageAsync(ScriptedNetworkSource source)
    {
        var page = new NetworkSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return page;
    }

    /// <summary>Loading the page makes it ready and sends nothing: a run needs the user's request.</summary>
    [Fact]
    public async Task LoadingNeverRunsTheDiagnostics()
    {
        var source = new ScriptedNetworkSource();
        var page = new NetworkSettingsPage(source);
        page.State.ShouldBe(SettingsPageState.Loading);
        page.CanRun.ShouldBeFalse("the page is not ready yet");

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        source.RunCount.ShouldBe(0);
        page.CanRun.ShouldBeTrue();
        page.IsNotRun.ShouldBeTrue();
        page.HasReport.ShouldBeFalse();
        page.Rows.ShouldBeEmpty();
        page.Announcement.ShouldBeEmpty();
        page.Section.ShouldBe(SettingsSection.Network);
        page.Title.ShouldBe(NetworkCopy.Title);
    }

    /// <summary>A run shows one row per endpoint in the source's order, with words for the status and the latency, and announces the count that answered.</summary>
    [Fact]
    public async Task ARunShowsOneRowPerEndpointAndAnnouncesTheResult()
    {
        var source = new ScriptedNetworkSource();
        var page = await ReadyPageAsync(source);
        var announced = PropertyChangeRecorder.Record(page);

        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.RunCount.ShouldBe(1);
        page.IsRunning.ShouldBeFalse();
        page.HasReport.ShouldBeTrue();
        page.IsNotRun.ShouldBeFalse();
        page.Rows.Select(row => row.Name).ToArray().ShouldBe(["openrouter", "ollama-local", "slow"]);
        page.Rows.Select(row => row.StatusLabel).ToArray().ShouldBe([NetworkCopy.StatusReachable, NetworkCopy.StatusUnreachable, NetworkCopy.StatusTimedOut]);
        page.Rows[0].Detail.ShouldBe("The endpoint answered with HTTP 401. 120 ms.");
        page.Rows[1].Detail.ShouldBe(NetworkCopy.CheckMessage(EndpointStatus.Unreachable, null));
        page.Rows[0].Summary.ShouldBe("openrouter, openrouter.ai: Reachable. The endpoint answered with HTTP 401. 120 ms.");
        page.Announcement.ShouldBe("Diagnostics finished. 1 of 3 endpoints reachable.");
        page.CheckedAtText.ShouldBe("Checked 2026-10-26 09:30:00 UTC.");
        announced.ShouldAnnounce(nameof(NetworkSettingsPage.Rows));
        announced.ShouldAnnounce(nameof(NetworkSettingsPage.Announcement));
        announced.ShouldAnnounce(nameof(NetworkSettingsPage.CanRun));
    }

    /// <summary>While a run is in progress the command is disabled, a second request does nothing, and the page announces that it is running.</summary>
    [Fact]
    public async Task TheCommandIsDisabledWhileARunIsInProgress()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new ScriptedNetworkSource { Gate = gate };
        var page = await ReadyPageAsync(source);

        var run = page.RunDiagnosticsAsync(TestContext.Current.CancellationToken);

        page.IsRunning.ShouldBeTrue();
        page.CanRun.ShouldBeFalse();
        page.Announcement.ShouldBe(NetworkCopy.Running);
        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.RunCount.ShouldBe(1, "a second request while running starts nothing");

        gate.SetResult();
        await run.ConfigureAwait(true);

        page.IsRunning.ShouldBeFalse();
        page.CanRun.ShouldBeTrue();
        page.Rows.Count.ShouldBe(3);
    }

    /// <summary>Cancelling a run ends it with the cancelled announcement, keeps the previous result, and the command works again.</summary>
    [Fact]
    public async Task CancellingARunKeepsThePreviousResult()
    {
        var source = new ScriptedNetworkSource();
        var page = await ReadyPageAsync(source);
        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Report = new NetworkDiagnosticsReport([], DateTimeOffset.UnixEpoch);

        var run = page.RunDiagnosticsAsync(TestContext.Current.CancellationToken);
        page.IsRunning.ShouldBeTrue();
        page.CancelRun();
        await run.ConfigureAwait(true);

        page.IsRunning.ShouldBeFalse();
        page.CanRun.ShouldBeTrue();
        page.Announcement.ShouldBe(NetworkCopy.Cancelled);
        page.Rows.Count.ShouldBe(3, "the cancelled run reported nothing");
        page.CheckedAtText.ShouldBe("Checked 2026-10-26 09:30:00 UTC.");
    }

    /// <summary>The caller's own token cancels a run too, and a cancel with no run in progress does nothing.</summary>
    [Fact]
    public async Task TheCallersTokenCancelsTheRun()
    {
        var source = new ScriptedNetworkSource { Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var page = await ReadyPageAsync(source);
        page.CancelRun();
        using var caller = new CancellationTokenSource();

        var run = page.RunDiagnosticsAsync(caller.Token);
        await caller.CancelAsync().ConfigureAwait(true);
        await run.ConfigureAwait(true);

        page.Announcement.ShouldBe(NetworkCopy.Cancelled);
        page.IsRunning.ShouldBeFalse();
        page.HasReport.ShouldBeFalse();
    }

    /// <summary>A source that fails with a reason shows that reason, and any other fault shows the general sentence; neither is thrown.</summary>
    [Fact]
    public async Task AFailedRunNeverThrows()
    {
        var source = new ScriptedNetworkSource { Fault = new SettingsSourceException("The provider list could not be read.") };
        var page = await ReadyPageAsync(source);

        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Announcement.ShouldBe("The provider list could not be read.");
        page.IsRunning.ShouldBeFalse();
        page.CanRun.ShouldBeTrue();
        page.HasReport.ShouldBeFalse();

        source.Fault = new InvalidOperationException("secret detail that must not be shown");
        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Announcement.ShouldBe(NetworkCopy.RunFailed);
    }

    /// <summary>A run that finds nothing to check says so and shows no row.</summary>
    [Fact]
    public async Task ARunWithNoEndpointsSaysSo()
    {
        var source = new ScriptedNetworkSource { Report = new NetworkDiagnosticsReport([], DateTimeOffset.UnixEpoch) };
        var page = await ReadyPageAsync(source);

        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.IsEmpty.ShouldBeTrue();
        page.Rows.ShouldBeEmpty();
        page.Announcement.ShouldBe(NetworkCopy.NoEndpoints);
    }

    /// <summary>The page never shows a scheme, a path, a query or credentials even when a source hands over a full URL, and hides what is no host.</summary>
    [Fact]
    public async Task AnEndpointIsShownAsHostAndPortOnly()
    {
        var source = new ScriptedNetworkSource
        {
            Report = new NetworkDiagnosticsReport(
            [
                new EndpointCheck("a", "https://user:hunter2@api.example.com:8443/v1?key=abc#frag", EndpointStatus.Reachable, 200, null, "ok"),
                new EndpointCheck("b", "not a host at all", EndpointStatus.NotChecked, null, null, "skipped"),
            ],
            DateTimeOffset.UnixEpoch),
        };
        var page = await ReadyPageAsync(source);

        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Rows[0].Host.ShouldBe("api.example.com:8443");
        page.Rows[1].Host.ShouldBe(NetworkCopy.HostHidden);
        page.Rows.Select(row => row.Summary).ShouldAllBe(summary => !summary.Contains("hunter2", StringComparison.Ordinal) && !summary.Contains("key=", StringComparison.Ordinal));
    }

    /// <summary>Before the page is ready a request does nothing.</summary>
    [Fact]
    public async Task ANotReadyPageDoesNotRun()
    {
        var source = new ScriptedNetworkSource();
        var page = new NetworkSettingsPage(source);

        await page.RunDiagnosticsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.RunCount.ShouldBe(0);
        page.Announcement.ShouldBeEmpty();
    }

    /// <summary>There is no Save: the page is never dirty and saving succeeds without a write.</summary>
    [Fact]
    public async Task ThereIsNothingToSave()
    {
        var token = TestContext.Current.CancellationToken;
        var page = await ReadyPageAsync(new ScriptedNetworkSource());
        await page.RunDiagnosticsAsync(token).ConfigureAwait(true);

        page.Reset();
        page.Discard();

        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        typeof(INetworkDiagnosticsSource).GetMethods().Select(method => method.Name).ToArray().ShouldBe(["RunAsync"]);
    }

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new NetworkSettingsPage(null!));
}
