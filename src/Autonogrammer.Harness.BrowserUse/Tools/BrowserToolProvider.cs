using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Content;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Vision;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>
/// The nine-tool provider. ProviderId is browser. Stage 2 asks through the broker. Skip never skips stage 2.
/// </summary>
public sealed class BrowserToolProvider : ICoworkToolProvider
{
    private readonly BrowserUseOptions _options;
    private readonly ICoworkApprovalBroker _broker;
    private readonly ICoworkGrantStore _grants;
    private readonly IBrowserPageSource _pages;
    private readonly IBrowserCallGate _gate;
    private readonly ILocalPortPermit _ports;
    private readonly OriginGrants _originGrants;
    private readonly BrowserLimits _limits;
    private readonly ElementRefTable _refs = new();
    private readonly BrowserApprovalStage2 _stage2;
    private readonly HashSet<CoworkGrantKey> _issued = [];
    private readonly Func<bool> _manual;
    private readonly Func<bool> _hasLocalUser;
    private readonly string? _profileDirectory;
    private readonly int _frameWidth = ScreenshotPipeline.ViewportWidth;
    private readonly int _frameHeight = ScreenshotPipeline.ViewportHeight;

    /// <summary>Initializes the provider.</summary>
    /// <param name="options">The options.</param>
    /// <param name="broker">The stage-2 broker.</param>
    /// <param name="grants">The grant store EndSession forgets.</param>
    /// <param name="pages">The page source. Preview does not open it.</param>
    /// <param name="gate">The call gate. Stop cancels through it.</param>
    /// <param name="ports">The local port permit.</param>
    /// <param name="readiness">The locate result. Null is available when enabled.</param>
    /// <param name="vision">The mutable vision flag. Null follows each session.</param>
    /// <param name="screenshots">The screenshot pipeline.</param>
    /// <param name="profileDirectory">The profile EndSession wipes.</param>
    /// <param name="manual">True when every click must ask.</param>
    /// <param name="hasLocalUser">False denies MustBeLocal on a dispatch turn.</param>
    /// <param name="time">The clock for limits.</param>
    public BrowserToolProvider(
        BrowserUseOptions options,
        ICoworkApprovalBroker broker,
        ICoworkGrantStore grants,
        IBrowserPageSource pages,
        IBrowserCallGate gate,
        ILocalPortPermit ports,
        CoworkProviderState? readiness = null,
        BrowserVisionAuthority? vision = null,
        ScreenshotPipeline? screenshots = null,
        string? profileDirectory = null,
        Func<bool>? manual = null,
        Func<bool>? hasLocalUser = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(ports);
        _options = options;
        _broker = broker;
        _grants = grants;
        _pages = pages;
        _gate = gate;
        _ports = ports;
        _originGrants = new OriginGrants();
        _limits = new BrowserLimits(options);
        Readiness = readiness ?? new CoworkProviderState(CoworkProviderAvailability.Available, null);
        Vision = vision ?? new BrowserVisionAuthority();
        Screenshots = screenshots ?? new ScreenshotPipeline(time);
        _profileDirectory = profileDirectory;
        _manual = manual ?? (static () => false);
        _hasLocalUser = hasLocalUser ?? (static () => true);
        _stage2 = new BrowserApprovalStage2(broker, ports, _originGrants);
        Clock = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string ProviderId => BrowserToolDescriptors.ProviderId;

    /// <summary>Gets the readiness used by GetStateAsync.</summary>
    public CoworkProviderState Readiness { get; }

    /// <summary>Gets the vision authority. A mid-session switch updates it.</summary>
    public BrowserVisionAuthority Vision { get; }

    /// <summary>Gets the screenshot pipeline.</summary>
    public ScreenshotPipeline Screenshots { get; }

    /// <summary>Gets the limits.</summary>
    public BrowserLimits Limits => _limits;

    /// <summary>Gets the element refs.</summary>
    public ElementRefTable Refs => _refs;

    /// <summary>Gets the stage-2 asks.</summary>
    public BrowserApprovalStage2 Stage2 => _stage2;

    /// <summary>Gets the origin grants.</summary>
    public OriginGrants OriginGrants => _originGrants;

    private TimeProvider Clock { get; }

    /// <inheritdoc />
    public IReadOnlyList<CoworkToolDescriptor> Describe() => BrowserToolDescriptors.All();

    /// <inheritdoc />
    public ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
        {
            return ValueTask.FromResult(new CoworkProviderState(CoworkProviderAvailability.NeedsSetup, "BrowserDisabled"));
        }

        return ValueTask.FromResult(Readiness);
    }

    /// <inheritdoc />
    public ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.Equals(invocation.ToolName, BrowserNavigateTool.Name, StringComparison.Ordinal) && BrowserNavigateTool.TryReadUrl(invocation.Arguments, out var url) && url is not null)
        {
            var target = BrowserApprovalStage2.UrlWithoutQuery(url);
            var grant = new CoworkGrantKey("browser.origin", url.GetLeftPart(UriPartial.Authority));
            return ValueTask.FromResult(new CoworkCallPreview("Open this origin?", target, [], grant));
        }

        return ValueTask.FromResult(new CoworkCallPreview(invocation.ToolName, null, [], null));
    }

    /// <inheritdoc />
    public async ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(invocation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _gate.Cancellation);
        await _gate.WaitIfTakenOverAsync(linked.Token).ConfigureAwait(false);
        if (!Vision.Reads(session))
        {
            session = session with { ModelReadsImages = false };
        }

        if (string.Equals(invocation.ToolName, BrowserScreenshotTool.Name, StringComparison.Ordinal) && !session.ModelReadsImages)
        {
            return CoworkToolResult.Failure(BrowserScreenshotTool.ModelCannotSeeImages);
        }

        if (!_limits.AllowsTurn(invocation.Origin))
        {
            return CoworkToolResult.Failure("DispatchBlocked");
        }

        if (!_limits.TryAdmit(Clock.GetUtcNow(), out var limit))
        {
            return CoworkToolResult.Failure(limit ?? "RateLimited");
        }

        var page = await _pages.OpenAsync(session.SessionId, linked.Token).ConfigureAwait(false);
        return invocation.ToolName switch
        {
            BrowserNavigateTool.Name => await NavigateAsync(session, invocation, page, linked.Token).ConfigureAwait(false),
            BrowserClickTool.Name => await ClickAsync(session, invocation, page, linked.Token).ConfigureAwait(false),
            BrowserTypeTool.Name => await TypeAsync(session, invocation, page, linked.Token).ConfigureAwait(false),
            BrowserScrollTool.Name => await ScrollAsync(page, linked.Token).ConfigureAwait(false),
            BrowserReadTool.Name => await ReadAsync(invocation, page, linked.Token).ConfigureAwait(false),
            BrowserScreenshotTool.Name => await ShotAsync(session, invocation, page, linked.Token).ConfigureAwait(false),
            BrowserWaitTool.Name => Wait(invocation),
            BrowserBackTool.Name => await BackAsync(page, linked.Token).ConfigureAwait(false),
            BrowserCloseTool.Name => await CloseAsync(page, linked.Token).ConfigureAwait(false),
            _ => CoworkToolResult.Failure("The tool is not offered."),
        };
    }

    /// <inheritdoc />
    public async ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        _ = sessionId;
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var grant in _issued.ToArray())
        {
            await _grants.RevokeAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        foreach (var grant in _stage2.StoredGrants)
        {
            await _grants.RevokeAsync(grant, cancellationToken).ConfigureAwait(false);
        }

        _issued.Clear();
        _stage2.Forget();
        if (!string.IsNullOrEmpty(_profileDirectory) && Directory.Exists(_profileDirectory))
        {
            Directory.Delete(_profileDirectory, recursive: true);
        }
    }

    /// <summary>Applies download, permission, dialog, popup and file-chooser guards.</summary>
    /// <param name="session">The page.</param>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The task.</returns>
    public async Task ApplyGuardsAsync(ICDPSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        var behavior = _options.DownloadMode == DownloadMode.Deny ? "deny" : "allow";
        var downloadPath = _options.DownloadMode == DownloadMode.Quarantine && _profileDirectory is not null
            ? BrowserDownloadQuarantine.EnsureDirectory(_profileDirectory)
            : string.Empty;
        _ = await session.SendAsync("Browser.setDownloadBehavior", new { behavior, downloadPath, eventsEnabled = true }, waitForCallback: true, options: null).ConfigureAwait(false);
        foreach (var name in new[] { "camera", "microphone", "geolocation", "notifications", "clipboard" })
        {
            _ = await session.SendAsync("Browser.setPermission", new { permission = new { name }, setting = "denied" }, waitForCallback: true, options: null).ConfigureAwait(false);
        }

        _ = await session.SendAsync("Page.setInterceptFileChooserDialog", new { enabled = true }, waitForCallback: true, options: null).ConfigureAwait(false);
    }

    private async Task<CoworkToolResult> NavigateAsync(CoworkSessionContext session, CoworkToolInvocation invocation, ICDPSession page, CancellationToken cancellationToken)
    {
        if (!BrowserNavigateTool.TryReadUrl(invocation.Arguments, out var url) || url is null)
        {
            return CoworkToolResult.Failure("The tool arguments are not a JSON object within the size cap.");
        }

        _issued.Add(new CoworkGrantKey("browser.origin", url.GetLeftPart(UriPartial.Authority)));

        var final = await BrowserNavigateTool.GoAsync(page, url, cancellationToken).ConfigureAwait(false);
        if (!SameOrigin(url, final))
        {
            var mode = _limits.EffectivePolicy(invocation.Origin);
            var allowed = await _stage2.AskRedirectAsync(session.SessionId, invocation.CallId, invocation.ToolName, final, mode, invocation.Origin, cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return CoworkToolResult.Failure("The tool call was not approved.");
            }
        }

        return PageText("navigated");
    }

    private async Task<CoworkToolResult> ClickAsync(CoworkSessionContext session, CoworkToolInvocation invocation, ICDPSession page, CancellationToken cancellationToken)
    {
        if (BrowserApprovalStage2.AsksEveryClick(_manual()))
        {
            var asked = await _stage2.AskSensitiveAsync(session.SessionId, invocation.CallId, invocation.ToolName, "Click", invocation.Origin, _hasLocalUser(), cancellationToken).ConfigureAwait(false);
            if (!asked)
            {
                return CoworkToolResult.Failure("The tool call was not approved.");
            }
        }

        if (invocation.Arguments.TryGetProperty("ref", out var refId))
        {
            if (!_refs.TryResolve(refId.GetString() ?? string.Empty, _refs.Generation, out var resolved))
            {
                return CoworkToolResult.Failure(ElementRefTable.StaleRef);
            }

            await BrowserClickTool.ClickAsync(page, resolved.X, resolved.Y, cancellationToken).ConfigureAwait(false);
            return CoworkToolResult.Text("clicked " + resolved.Role);
        }

        var x = invocation.Arguments.TryGetProperty("x", out var xElement) ? xElement.GetDouble() : 0;
        var y = invocation.Arguments.TryGetProperty("y", out var yElement) ? yElement.GetDouble() : 0;
        var scaled = ElementRefTable.Scale(x, y, _frameWidth, _frameHeight, ScreenshotPipeline.ViewportWidth, ScreenshotPipeline.ViewportHeight);
        await BrowserClickTool.ClickAsync(page, scaled.X, scaled.Y, cancellationToken).ConfigureAwait(false);
        return CoworkToolResult.Text("clicked");
    }

    private async Task<CoworkToolResult> TypeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, ICDPSession page, CancellationToken cancellationToken)
    {
        var sensitive = invocation.Arguments.TryGetProperty("sensitive", out var flag) && flag.ValueKind == JsonValueKind.True;
        if (sensitive || BrowserApprovalStage2.AsksEveryClick(_manual()))
        {
            var asked = await _stage2.AskSensitiveAsync(session.SessionId, invocation.CallId, invocation.ToolName, "Type into a field", invocation.Origin, _hasLocalUser(), cancellationToken).ConfigureAwait(false);
            if (!asked)
            {
                return CoworkToolResult.Failure("The tool call was not approved.");
            }
        }

        var submit = invocation.Arguments.TryGetProperty("submit", out var submitElement) && submitElement.ValueKind == JsonValueKind.True;
        if (submit && _stage2.NeedsSubmitAsk("session", sensitive))
        {
            var asked = await _stage2.AskSensitiveAsync(session.SessionId, invocation.CallId, invocation.ToolName, "Submit the form", invocation.Origin, _hasLocalUser(), cancellationToken).ConfigureAwait(false);
            if (!asked)
            {
                return CoworkToolResult.Failure("The tool call was not approved.");
            }
        }

        await BrowserTypeTool.TypeAsync(page, invocation.Arguments.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty, submit, cancellationToken).ConfigureAwait(false);
        return CoworkToolResult.Text("field accepted");
    }

    private static async Task<CoworkToolResult> ScrollAsync(ICDPSession page, CancellationToken cancellationToken)
    {
        await BrowserScrollTool.ScrollAsync(page, 100, cancellationToken).ConfigureAwait(false);
        return CoworkToolResult.Text("scrolled");
    }

    private static async Task<CoworkToolResult> ReadAsync(CoworkToolInvocation invocation, ICDPSession page, CancellationToken cancellationToken)
    {
        var view = invocation.Arguments.TryGetProperty("view", out var viewElement) ? viewElement.GetString() ?? "text" : "text";
        var marker = new PageMarker();
        var text = await BrowserReadTool.ReadAsync(page, view, marker, cancellationToken).ConfigureAwait(false);
        return new CoworkToolResult([new CoworkTextBlock(text, PageContentExtractor.IsUntrusted)], false);
    }

    private async Task<CoworkToolResult> ShotAsync(CoworkSessionContext session, CoworkToolInvocation invocation, ICDPSession page, CancellationToken cancellationToken)
    {
        if (!_limits.TryRecordShot())
        {
            return CoworkToolResult.Failure("ImageBudgetExhausted. Allow 20 more");
        }

        var viewport = invocation.Arguments.TryGetProperty("viewport", out var view) ? view.GetString() ?? "desktop" : "desktop";
        var zoom = invocation.Arguments.TryGetProperty("zoom", out var zoomElement) && zoomElement.ValueKind == JsonValueKind.True;
        Task<byte[]> CaptureShotAsync(ScreenshotStep step, CancellationToken token) => BrowserScreenshotTool.CaptureAsync(page, step, token);
        var shot = zoom
            ? await Screenshots.ZoomAsync(CaptureShotAsync, _limits.MaxScreenshotBytes, cancellationToken).ConfigureAwait(false)
            : await Screenshots.CaptureAsync(CaptureShotAsync, _limits.MaxScreenshotBytes, viewport, cancellationToken).ConfigureAwait(false);
        if (shot.Failure is not null)
        {
            return CoworkToolResult.Failure(shot.Failure);
        }

        var origin = "unknown origin";
        var image = new CoworkImageBlock(shot.Bytes, shot.MimeType, PageMarker.ImageDescription(origin), true);
        var text = new CoworkTextBlock("screenshot", true);
        _ = session;
        return new CoworkToolResult([text, image], false);
    }

    private static CoworkToolResult Wait(CoworkToolInvocation invocation)
    {
        var seconds = invocation.Arguments.TryGetProperty("seconds", out var value) && value.TryGetDouble(out var parsed) ? parsed : 0;
        return CoworkToolResult.Text("waited " + BrowserWaitTool.Cap(seconds).TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task<CoworkToolResult> BackAsync(ICDPSession page, CancellationToken cancellationToken)
    {
        await BrowserBackTool.BackAsync(page, cancellationToken).ConfigureAwait(false);
        return CoworkToolResult.Text("back");
    }

    private static async Task<CoworkToolResult> CloseAsync(ICDPSession page, CancellationToken cancellationToken)
    {
        await BrowserCloseTool.CloseAsync(page, cancellationToken).ConfigureAwait(false);
        return CoworkToolResult.Text("closed");
    }

    private static CoworkToolResult PageText(string text) => new([new CoworkTextBlock(text, true)], false);

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.GetLeftPart(UriPartial.Authority), right.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
}

