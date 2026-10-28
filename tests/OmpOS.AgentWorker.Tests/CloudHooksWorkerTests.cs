using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.AgentWorker.Bridge;
using OmpOS.Harness.Acp;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>
/// Hooks in a cloud worker, end to end over the loopback WebSocket with a fake omp: the consent snapshot in the token decides which project hooks run,
/// a denying hook stops a tool call through the bridge the omp extension would call, and every hook run reaches the client as an <c>_ompos/hook</c>
/// notification. The tests play the omp extension by posting to the bridge themselves.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class CloudHooksWorkerTests
{
    private const string Session = "session-hooks";
    private const string InitializeRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":1}}";
    private const string PingRequest = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}";

    private static AcpHookNotification? Notification(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("method", out var method) || !string.Equals(method.GetString(), AcpHookNotificationFrames.Method, StringComparison.Ordinal))
        {
            return null;
        }

        return AcpHookNotificationFrames.TryParse(document.RootElement.GetProperty("params").Clone(), out var notification) ? notification : null;
    }

    private static bool IsReply(string text, int id) =>
        text.Contains("\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal) && text.Contains("\"result\"", StringComparison.Ordinal);

    private static async Task<List<string>> CollectAsync(ClientWebSocket socket, Func<IReadOnlyList<string>, bool> done, CancellationToken cancellationToken)
    {
        var texts = new List<string>();
        while (!done(texts))
        {
            var message = await WorkerSocket.ReceiveAsync(socket, cancellationToken).ConfigureAwait(true);
            if (message.Type == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("The worker closed the connection: " + message.CloseDescription);
            }

            texts.Add(message.Text);
        }

        return texts;
    }

    private static bool HasState(IReadOnlyList<string> texts, string state) =>
        texts.Any(text => Notification(text) is { State: { } found } && string.Equals(found, state, StringComparison.Ordinal));

    private static async Task<JsonDocument> PostToBridgeAsync(string url, string token, string body, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Absolute)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(true);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(true));
    }

    private static async Task<WorkerHost> StartAsync(WorkerTokenFactory tokens, FixedTimeProvider clock, CloudHooksFixture fixture, bool enabled, CancellationToken cancellationToken) =>
        await WorkerHost.StartAsync(tokens, clock, fixture.OmpArguments, settings: fixture.Settings(enabled), cancellationToken: cancellationToken).ConfigureAwait(true);

    /// <summary>A project hook the user approved denies a tool call in the cloud: the bridge answers deny with the hook's message, and the denied run arrives as a notification.</summary>
    [Fact]
    public async Task ADenyingProjectHookStopsAToolCallInTheCloud()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: true, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var repository = CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, CloudHooksFixture.DenyHooksJson);
        var hashes = await CloudHooksFixture.HashesAsync(repository, timeout.Token).ConfigureAwait(true);
        hashes.Length.ShouldBe(1);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot(hashes));
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        var first = await CollectAsync(socket, static texts => texts.Any(static text => IsReply(text, 1)), timeout.Token).ConfigureAwait(true);
        var (url, bridgeToken) = await fixture.ReadBridgeAsync(timeout.Token).ConfigureAwait(true);
        using var decision = await PostToBridgeAsync(url, bridgeToken, CloudHooksFixture.ShellRequest, timeout.Token).ConfigureAwait(true);
        var rest = await CollectAsync(socket, static texts => texts.Any(static text => Notification(text) is { Run: { Status: "denied" } }), timeout.Token).ConfigureAwait(true);

        decision.RootElement.GetProperty("permission").GetString().ShouldBe("deny");
        decision.RootElement.GetProperty("user_message").GetString().ShouldBe("Blocked by the project guard.");
        var notifications = first.Concat(rest).Select(Notification).OfType<AcpHookNotification>().ToArray();
        var state = notifications.Single(static notification => notification.State is not null);
        state.State.ShouldBe(AcpHookNotification.StateActive);
        state.HookCount.ShouldBe(1);
        var run = notifications.Single(static notification => notification.Run is not null).Run!;
        run.EventName.ShouldBe("beforeShellExecution");
        run.Scope.ShouldBe("project");
        run.Decision.ShouldBe("deny");
        run.SessionId.ShouldBe(Session);
        (await File.ReadAllTextAsync(fixture.ArgvFile, timeout.Token).ConfigureAwait(true)).ShouldContain("ompos-hooks-omp", Case.Sensitive);
        bridgeToken.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A connection without a consent snapshot runs no project hook: no bridge, no extension, the session says untrusted.</summary>
    [Fact]
    public async Task WithoutASnapshotNoProjectHookRunsAndTheSessionIsUntrusted()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: true, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, CloudHooksFixture.DenyHooksJson);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), consent: null), timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        var frames = await CollectAsync(socket, static texts => texts.Any(static text => IsReply(text, 1)) && HasState(texts, "untrusted"), timeout.Token).ConfigureAwait(true);

        var state = frames.Select(Notification).OfType<AcpHookNotification>().Single();
        state.HookCount.ShouldBe(1);
        state.Reason.ShouldNotBeNullOrWhiteSpace();
        (await fixture.ReadBridgeAsync(timeout.Token).ConfigureAwait(true)).ShouldBe((string.Empty, string.Empty));
        (await File.ReadAllTextAsync(fixture.ArgvFile, timeout.Token).ConfigureAwait(true)).ShouldNotContain("ompos-hooks-omp", Case.Sensitive);
    }

    /// <summary>A claim that is present but invalid is refused as a whole: the session runs no hook and reports the fixed reason, never the claim.</summary>
    [Fact]
    public async Task AnInvalidSnapshotRunsNoHookAndNamesAFixedReason()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: true, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, CloudHooksFixture.DenyHooksJson);
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), "secret-looking text, not json"), timeout.Token).ConfigureAwait(true);

        var frames = await CollectAsync(socket, static texts => HasState(texts, "untrusted"), timeout.Token).ConfigureAwait(true);

        var state = frames.Select(Notification).OfType<AcpHookNotification>().Single();
        state.Reason.ShouldBe("The consent claim is not a valid snapshot.");
        frames.ShouldAllBe(static text => !text.Contains("secret-looking", StringComparison.Ordinal));
    }

    /// <summary>A snapshot that does not hold the hook's identity keeps the rule of ADR-0016: the guarding hook denies the action without running, and the run says it waits for consent.</summary>
    [Fact]
    public async Task AHookTheSnapshotDoesNotCoverDeniesWithoutRunning()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: true, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, CloudHooksFixture.DenyHooksJson);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot(new string('f', 64)));
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        await CollectAsync(socket, static texts => texts.Any(static text => IsReply(text, 1)), timeout.Token).ConfigureAwait(true);
        var (url, bridgeToken) = await fixture.ReadBridgeAsync(timeout.Token).ConfigureAwait(true);
        using var decision = await PostToBridgeAsync(url, bridgeToken, CloudHooksFixture.ShellRequest, timeout.Token).ConfigureAwait(true);
        var frames = await CollectAsync(socket, static texts => texts.Any(static text => Notification(text) is { Run: not null }), timeout.Token).ConfigureAwait(true);

        decision.RootElement.GetProperty("permission").GetString().ShouldBe("deny");
        var run = frames.Select(Notification).OfType<AcpHookNotification>().First(static notification => notification.Run is not null).Run!;
        run.Status.ShouldBe("awaitingConsent");
        run.ExitCode.ShouldBeNull();
        run.PayloadSha256.ShouldBeNull();
    }

    /// <summary>With <c>Worker:Hooks:Enabled</c> off (the default) a snapshot changes nothing: no hook frame, no bridge, no extension.</summary>
    [Fact]
    public async Task HooksAreOffByDefaultEvenWithASnapshot()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: false, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var repository = CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, CloudHooksFixture.DenyHooksJson);
        var hashes = await CloudHooksFixture.HashesAsync(repository, timeout.Token).ConfigureAwait(true);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot(hashes));
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true);

        await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
        await WorkerSocket.SendTextAsync(socket, PingRequest, timeout.Token).ConfigureAwait(true);
        var frames = await CollectAsync(socket, static texts => texts.Any(static text => IsReply(text, 2)), timeout.Token).ConfigureAwait(true);

        frames.Select(Notification).OfType<AcpHookNotification>().ShouldBeEmpty();
        (await fixture.ReadBridgeAsync(timeout.Token).ConfigureAwait(true)).ShouldBe((string.Empty, string.Empty));
        (await File.ReadAllTextAsync(fixture.ArgvFile, timeout.Token).ConfigureAwait(true)).ShouldNotContain("ompos-hooks-omp", Case.Sensitive);
    }

    /// <summary>A checkout without project hooks reports none and starts no bridge.</summary>
    [Fact]
    public async Task ACheckoutWithoutHooksReportsNone()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: true, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, hooksJson: null);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot());
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true);

        var frames = await CollectAsync(socket, static texts => HasState(texts, "none"), timeout.Token).ConfigureAwait(true);

        frames.Select(Notification).OfType<AcpHookNotification>().Single().HookCount.ShouldBe(0);
    }

    /// <summary>Hooks of events a cloud worker does not run are not counted or run: a file with a session-start hook and one shell hook loads one.</summary>
    [Fact]
    public async Task OnlyTheCloudEventsOfAHookFileAreLoaded()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await StartAsync(tokens, clock, fixture, enabled: true, timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        const string hooks = """
            {"version":1,"hooks":{
              "sessionStart":[{"command":"true"}],
              "beforeShellExecution":[{"command":"true"}]
            }}
            """;
        var repository = CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, hooks);
        var hashes = await CloudHooksFixture.HashesAsync(repository, timeout.Token).ConfigureAwait(true);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot(hashes));
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true);

        var frames = await CollectAsync(socket, static texts => HasState(texts, "active"), timeout.Token).ConfigureAwait(true);

        frames.Select(Notification).OfType<AcpHookNotification>().Single().HookCount.ShouldBe(1);
    }

    /// <summary>When a hook that must block cannot be enforced because the extension is missing, the connection is refused with 503 instead of running tools unguarded, and the session is freed.</summary>
    [Fact]
    public async Task AFailClosedHookWithoutTheExtensionRefusesTheConnection()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var settings = new Dictionary<string, string?>(fixture.Settings(enabled: true), StringComparer.Ordinal)
        {
            ["Worker:Hooks:PluginsRoot"] = Path.Combine(fixture.Directory, "no-plugins-here"),
        };
        var host = await WorkerHost.StartAsync(tokens, clock, fixture.OmpArguments, settings: settings, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var repository = CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, CloudHooksFixture.DenyHooksJson);
        var hashes = await CloudHooksFixture.HashesAsync(repository, timeout.Token).ConfigureAwait(true);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot(hashes));

        var failure = await Should.ThrowAsync<WebSocketException>(() => WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token)).ConfigureAwait(true);

        failure.Message.ShouldContain("503", Case.Sensitive);
        host.Services.GetRequiredService<OmpProcessHost>().AttachedCount.ShouldBe(0);
    }

    /// <summary>A hook that does not fail closed and has no extension to run it is reported as unavailable, and the session still starts.</summary>
    [Fact]
    public async Task AnAdvisoryHookWithoutTheExtensionIsReportedUnavailable()
    {
        using var timeout = TestTimeouts.Create();
        using var fixture = new CloudHooksFixture();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var settings = new Dictionary<string, string?>(fixture.Settings(enabled: true), StringComparer.Ordinal)
        {
            ["Worker:Hooks:PluginsRoot"] = Path.Combine(fixture.Directory, "no-plugins-here"),
        };
        var host = await WorkerHost.StartAsync(tokens, clock, fixture.OmpArguments, settings: settings, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        const string hooks = "{\"version\":1,\"hooks\":{\"afterShellExecution\":[{\"command\":\"true\"}]}}";
        var repository = CloudHooksFixture.WriteCheckout(host.WorkspaceRoot, Session, hooks);
        var hashes = await CloudHooksFixture.HashesAsync(repository, timeout.Token).ConfigureAwait(true);
        var token = HooksTokenMinter.Mint(tokens, Session, clock.GetUtcNow(), HooksTokenMinter.Snapshot(hashes));
        using var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true);

        var frames = await CollectAsync(socket, static texts => HasState(texts, "unavailable"), timeout.Token).ConfigureAwait(true);

        frames.Select(Notification).OfType<AcpHookNotification>().Single().HookCount.ShouldBe(1);
    }
}
