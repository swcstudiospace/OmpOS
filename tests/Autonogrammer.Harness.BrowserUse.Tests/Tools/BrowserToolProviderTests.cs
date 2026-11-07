using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Tools;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Tools;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Tools;

/// <summary>Provider tests through the real host, plus refs, vision and Stop.</summary>
public sealed class BrowserToolProviderTests
{
    [Fact]
    public async Task NineTools_MapToOmpNames_AndOnlyClickAndTypeActuate()
    {
        await using var harness = Harness.Create(enabled: true);
        var offered = await harness.Host.ListOfferedAsync("s", CancellationToken.None).ConfigureAwait(true);
        offered.Count.ShouldBe(9);
        offered.Select(item => item.Name).ShouldBe(BrowserToolDescriptors.All().Select(item => item.Name), ignoreOrder: true);
        offered.ShouldAllBe(item => string.Equals(BrowserToolDescriptors.OmpName(item.Name), "mcp__cowork_" + item.Name, StringComparison.Ordinal));
        offered.Where(item => item.Effect == CoworkToolEffect.Actuating).Select(item => item.Name).ShouldBe(["browser_click", "browser_type"], ignoreOrder: true);
        offered.Where(item => !string.Equals(item.Name, "browser_click", StringComparison.Ordinal) && !string.Equals(item.Name, "browser_type", StringComparison.Ordinal)).ShouldAllBe(item => item.Effect == CoworkToolEffect.ReadOnly);
        var navigate = offered.Single(item => string.Equals(item.Name, "browser_navigate", StringComparison.Ordinal));
        var preview = await harness.Provider.PreviewAsync(harness.Session, Invoke("browser_navigate", """{"url":"https://app.example/path?secret=1"}"""), CancellationToken.None).ConfigureAwait(true);
        preview.Grant.ShouldBe(new CoworkGrantKey("browser.origin", "https://app.example"));
        preview.Target.ShouldBe("https://app.example/path");
        preview.Target!.ShouldNotContain("?");
        harness.Pages.Opens.ShouldBe(0);
        navigate.Approval.ShouldBe(CoworkApprovalRequirement.ByPolicy);
    }

    [Fact]
    public async Task GetState_ReportsTheReason_AndPreviewDoesNotOpenThePage()
    {
        var disabled = new BrowserToolProvider(new BrowserUseOptions(), new AllowBroker(), new MemoryGrants(), new ScriptedPages(), new OpenGate(), new PortPermit());
        var state = await disabled.GetStateAsync(Session(true), CancellationToken.None).ConfigureAwait(true);
        state.Availability.ShouldBe(CoworkProviderAvailability.NeedsSetup);
        state.Reason.ShouldBe("BrowserDisabled");
        var tooOld = new BrowserToolProvider(
            new BrowserUseOptions { Enabled = true },
            new AllowBroker(),
            new MemoryGrants(),
            new ScriptedPages(),
            new OpenGate(),
            new PortPermit(),
            new CoworkProviderState(CoworkProviderAvailability.Unavailable, "BrowserTooOld"));
        (await tooOld.GetStateAsync(Session(true), CancellationToken.None).ConfigureAwait(true)).Reason.ShouldBe("BrowserTooOld");
    }

    [Fact]
    public async Task Skip_StillAsksForARedirectOrigin()
    {
        await using var harness = Harness.Create(enabled: true);
        harness.Pages.FinalUrl = "https://other.example/landed?token=1";
        var result = await harness.Host.InvokeAsync("s", "browser_navigate", Json("""{"url":"https://app.example/start"}"""), CancellationToken.None).ConfigureAwait(true);
        result.IsError.ShouldBeFalse();
        harness.Broker.Calls.ShouldContain(call => call.Preview.Grant!.Scope == "browser.origin" && call.Preview.Grant.Value == "https://other.example");
        harness.Broker.Calls.ShouldAllBe(call => call.Preview.Target == null || !call.Preview.Target.Contains('?', StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manual_AsksEveryClick()
    {
        await using var harness = Harness.Create(enabled: true, manual: true);
        var result = await harness.Host.InvokeAsync("s", "browser_click", Json("""{"x":1,"y":2}"""), CancellationToken.None).ConfigureAwait(true);
        result.IsError.ShouldBeFalse();
        harness.Broker.Calls.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task EndSession_WipesTheProfile_AndForgetsApprovals()
    {
        var profile = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ag-browser-" + Guid.NewGuid().ToString("N"))).FullName;
        await File.WriteAllTextAsync(Path.Combine(profile, "cookie"), "x", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var grants = new MemoryGrants();
        await grants.GrantAsync(new CoworkGrantKey("browser.origin", "https://app.example"), CancellationToken.None).ConfigureAwait(true);
        var provider = new BrowserToolProvider(new BrowserUseOptions { Enabled = true }, new AllowBroker(), grants, new ScriptedPages(), new OpenGate(), new PortPermit(), profileDirectory: profile);
        await provider.EndSessionAsync("s", CancellationToken.None).ConfigureAwait(true);
        Directory.Exists(profile).ShouldBeFalse();
    }

    [Fact]
    public async Task Stop_CancelsTheInFlightCall()
    {
        using var stop = new CancellationTokenSource();
        var gate = new OpenGate(stop.Token);
        var pages = new ScriptedPages { Block = true, OnOpen = () => stop.Cancel() };
        var provider = new BrowserToolProvider(new BrowserUseOptions { Enabled = true }, new AllowBroker(), new MemoryGrants(), pages, gate, new PortPermit());
        var cancelled = false;
        try
        {
            await provider.InvokeAsync(Session(true), Invoke("browser_navigate", """{"url":"https://app.example/"}"""), CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        cancelled.ShouldBeTrue();
    }

    [Fact]
    public void ElementRefs_FailWhenStale_AndCoordinatesScale()
    {
        var table = new ElementRefTable();
        table.Replace([new AxRefNode(7, "button", "Save", 10, 20)]);
        table.TryResolve("e1", table.Generation, out var resolved).ShouldBeTrue();
        resolved.BackendDomNodeId.ShouldBe(7);
        table.TryResolve("e1", table.Generation + 1, out _).ShouldBeFalse();
        var scaled = ElementRefTable.Scale(640, 400, 1280, 800, 1280, 800);
        scaled.X.ShouldBe(640);
        var shrunk = ElementRefTable.Scale(320, 200, 640, 400, 1280, 800);
        shrunk.X.ShouldBe(640);
        shrunk.Y.ShouldBe(400);
    }

    [Fact]
    public async Task ModelThatCannotSeeImages_WithholdsScreenshot_AndASwitchFailsTheCall()
    {
        await using var harness = Harness.Create(enabled: true, readsImages: false);
        var offered = await harness.Host.ListOfferedAsync("s", CancellationToken.None).ConfigureAwait(true);
        offered.ShouldNotContain(item => item.Name == "browser_screenshot");
        offered.Count.ShouldBe(8);
        (await harness.Provider.GetStateAsync(harness.Session, CancellationToken.None).ConfigureAwait(true)).Availability.ShouldBe(CoworkProviderAvailability.Available);
        var missing = await harness.Host.InvokeAsync("s", "browser_screenshot", Json("{}"), CancellationToken.None).ConfigureAwait(true);
        missing.IsError.ShouldBeTrue();
        Text(missing).ShouldBe(CoworkToolMessages.NotOffered);

        await using var seeing = Harness.Create(enabled: true, readsImages: true);
        (await seeing.Host.ListOfferedAsync("s", CancellationToken.None).ConfigureAwait(true)).ShouldContain(item => item.Name == "browser_screenshot");
        seeing.Provider.Vision.Set(false);
        var switched = await seeing.Host.InvokeAsync("s", "browser_screenshot", Json("{}"), CancellationToken.None).ConfigureAwait(true);
        Text(switched).ShouldContain("ModelCannotSeeImages");
        Text(switched).ShouldContain("Choose a vision model");
    }

    private static string Text(CoworkToolResult result) => ((CoworkTextBlock)result.Content[0]).Text;

    private static CoworkSessionContext Session(bool reads) => new()
    {
        SessionId = "s",
        WorkingDirectory = "/",
        TrustedFolders = [],
        ModelReadsImages = reads,
    };

    private static CoworkToolInvocation Invoke(string name, string json) =>
        new("s", "c", name, Json(json), CoworkTurnOrigin.Local);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private sealed class Harness : IAsyncDisposable
    {
        public required CoworkToolHost Host { get; init; }

        public required BrowserToolProvider Provider { get; init; }

        public required RecordingBroker Broker { get; init; }

        public required ScriptedPages Pages { get; init; }

        public required CoworkSessionContext Session { get; init; }

        public required ICoworkSessionLease Lease { get; init; }

        public static Harness Create(bool enabled, bool readsImages = true, bool manual = false)
        {
            var broker = new RecordingBroker();
            var pages = new ScriptedPages();
            var grants = new MemoryGrants();
            var provider = new BrowserToolProvider(
                new BrowserUseOptions { Enabled = enabled },
                broker,
                grants,
                pages,
                new OpenGate(),
                new PortPermit(),
                manual: () => manual);
            var host = new CoworkToolHost([provider], new CoworkPolicyGate(grants, () => CoworkPermissionMode.Skip), broker, grants, new MemoryAudit());
            var session = BrowserToolProviderTests.Session(readsImages);
            var lease = host.BeginSession(session);
            return new Harness { Host = host, Provider = provider, Broker = broker, Pages = pages, Session = session, Lease = lease };
        }

        public async ValueTask DisposeAsync() => await Lease.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class RecordingBroker : ICoworkApprovalBroker
    {
        public List<CoworkApprovalRequest> Calls { get; } = [];

        public ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return ValueTask.FromResult(new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed));
        }
    }

    private sealed class AllowBroker : ICoworkApprovalBroker
    {
        public ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed));
    }

    private sealed class MemoryGrants : ICoworkGrantStore
    {
        private readonly HashSet<CoworkGrantKey> _grants = [];

        public ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.FromResult(_grants.Contains(key));

        public ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken)
        {
            _grants.Add(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken)
        {
            _grants.Remove(key);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MemoryAudit : ICoworkToolAudit
    {
        public ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
    private sealed class OpenGate : IBrowserCallGate
    {
        public OpenGate(CancellationToken cancellation = default) => Cancellation = cancellation;

        public CancellationToken Cancellation { get; }

        public bool UserHasControl { get; set; }

        public Task WaitIfTakenOverAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PortPermit : ILocalPortPermit
    {
        public void Open(string host, int port) => _ = (host, port);
    }

    private sealed class ScriptedPages : IBrowserPageSource
    {
        public int Opens { get; private set; }

        public string FinalUrl { get; set; } = "https://app.example/start";

        public bool Block { get; set; }

        public Action? OnOpen { get; set; }

        public async ValueTask<ICDPSession> OpenAsync(string sessionId, CancellationToken cancellationToken)
        {
            _ = sessionId;
            Opens++;
            OnOpen?.Invoke();
            if (Block)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            return new ScriptedSession(FinalUrl);
        }
    }

    private sealed class ScriptedSession : ICDPSession
    {
        private readonly string _finalUrl;

        public ScriptedSession(string finalUrl) => _finalUrl = finalUrl;

        public ILoggerFactory LoggerFactory => Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        public string Id => "s";

        public string CloseReason => string.Empty;

        public bool Detached => false;

        public event EventHandler? Disconnected { add { } remove { } }

        public event EventHandler<SessionEventArgs>? SessionAttached { add { } remove { } }

        public event EventHandler<SessionEventArgs>? SessionDetached { add { } remove { } }

        public event EventHandler<MessageEventArgs>? MessageReceived { add { } remove { } }

        public Task DetachAsync() => Task.CompletedTask;

        public void Close(string closeReason) => _ = closeReason;

        public Task<T> SendAsync<T>(string method, object? args = null, CommandOptions? options = null)
        {
            _ = (method, args, options);
            return Task.FromResult<T>(default!);
        }

        public Task<JsonElement?> SendAsync(string method, object? args = null, bool waitForCallback = true, CommandOptions? options = null)
        {
            _ = (args, waitForCallback, options);
            if (string.Equals(method, "Page.navigate", StringComparison.Ordinal))
            {
                using var document = JsonDocument.Parse("{\"url\":\"" + _finalUrl + "\"}");
                return Task.FromResult<JsonElement?>(document.RootElement.Clone());
            }

            return Task.FromResult<JsonElement?>(null);
        }
    }
}
