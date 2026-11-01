using System.Globalization;
using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Egress;
using Autonogrammer.Harness.BrowserUse.Options;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Policy;

/// <summary>
/// Pauses every document request, including redirect hops, with raw Fetch.enable and aborts a denied target
/// before the request leaves. A new top-level origin raises a host-observed stage-2 card.
/// </summary>
public sealed class NavigationGate
{
    private readonly BrowserUseOptions _options;
    private readonly OriginGrants _grants;
    private readonly Uri? _requestedOrigin;
    private readonly Func<CoworkCallPreview, Task<bool>>? _approve;
    private readonly bool _dispatchTurn;

    /// <summary>Initializes the gate.</summary>
    /// <param name="options">The policy options.</param>
    /// <param name="grants">The session local-origin grants.</param>
    /// <param name="requestedOrigin">The stage-1 origin, or null.</param>
    /// <param name="approve">The stage-2 decision. Null refuses the card.</param>
    /// <param name="dispatchTurn">Whether Open is ignored.</param>
    public NavigationGate(
        BrowserUseOptions options,
        OriginGrants grants,
        Uri? requestedOrigin,
        Func<CoworkCallPreview, Task<bool>>? approve = null,
        bool dispatchTurn = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grants);
        _options = options;
        _grants = grants;
        _requestedOrigin = requestedOrigin;
        _approve = approve;
        _dispatchTurn = dispatchTurn;
    }

    /// <summary>Enables document pausing and handles each paused request.</summary>
    /// <param name="session">The raw CDP session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    public async Task AttachAsync(ICDPSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        session.MessageReceived += (_, args) =>
        {
            if (string.Equals(args.MessageID, "Fetch.requestPaused", StringComparison.Ordinal))
            {
                _ = HandlePausedAsync(session, args.MessageData);
            }
        };
        await session.SendAsync(
            "Fetch.enable",
            new { patterns = new[] { new { resourceType = "Document" } } },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    /// <summary>Builds the stage-2 card. Target omits the query. Details name the query length past 64 characters.</summary>
    /// <param name="url">The host-observed URL.</param>
    /// <returns>The card.</returns>
    public static CoworkCallPreview Stage2Card(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var data = url.Query.Length == 0 ? 0 : url.Query.Length - 1;
        IReadOnlyList<string> details = data > 64
            ? ["this address carries " + data.ToString(CultureInfo.InvariantCulture) + " characters of data"]
            : [];
        var host = url.HostNameType == UriHostNameType.IPv6 ? "[" + url.Host + "]" : url.Host;
        var port = url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture);
        var target = url.Scheme + "://" + host + port + url.AbsolutePath;
        var origin = url.GetLeftPart(UriPartial.Authority);
        return new CoworkCallPreview("Open this origin?", target, details, new CoworkGrantKey("browser.origin", origin));
    }

    private async Task HandlePausedAsync(ICDPSession session, JsonElement data)
    {
        var requestId = data.TryGetProperty("requestId", out var id) ? id.GetString() : null;
        var resourceType = data.TryGetProperty("resourceType", out var kind) ? kind.GetString() : null;
        var url = data.TryGetProperty("request", out var request) && request.TryGetProperty("url", out var urlElement)
            ? urlElement.GetString()
            : null;
        if (string.IsNullOrEmpty(requestId))
        {
            return;
        }

        if (!string.Equals(resourceType, "Document", StringComparison.Ordinal))
        {
            await ContinueAsync(session, requestId).ConfigureAwait(false);
            return;
        }

        if (!ShouldContinue(url, out var card))
        {
            await FailAsync(session, requestId).ConfigureAwait(false);
            return;
        }

        if (card is not null)
        {
            var approved = _approve is not null && await _approve(card).ConfigureAwait(false);
            if (!approved)
            {
                await FailAsync(session, requestId).ConfigureAwait(false);
                return;
            }
        }

        await ContinueAsync(session, requestId).ConfigureAwait(false);
    }

    private bool ShouldContinue(string? url, out CoworkCallPreview? card)
    {
        card = null;
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !BrowserUrlPolicy.IsTopLevelSchemeAllowed(uri))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsDestinationAllowed(uri))
        {
            return false;
        }

        if (IsStage1(uri) || BrowserUrlPolicy.EffectiveMode(_options, _dispatchTurn) == PolicyMode.Open)
        {
            return true;
        }

        card = Stage2Card(uri);
        return true;
    }

    private bool IsDestinationAllowed(Uri uri)
    {
        if (!BrowserUrlPolicy.IsHostListed(uri.IdnHost, _options, _dispatchTurn))
        {
            return false;
        }

        var literal = AddressClassifier.ClassifyLiteral(uri.IdnHost);
        if (string.Equals(literal.Reason, "unparsed", StringComparison.Ordinal))
        {
            return true;
        }

        if (!literal.IsRefused)
        {
            return true;
        }

        return _grants.Contains(uri.IdnHost, uri.Port) && OriginGrants.IsEligible(uri.IdnHost);
    }

    private bool IsStage1(Uri uri)
    {
        if (_requestedOrigin is null)
        {
            return false;
        }

        return string.Equals(_requestedOrigin.Scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_requestedOrigin.IdnHost, uri.IdnHost, StringComparison.OrdinalIgnoreCase)
            && _requestedOrigin.Port == uri.Port;
    }

    private static async Task ContinueAsync(ICDPSession session, string requestId)
    {
        _ = await session.SendAsync("Fetch.continueRequest", new { requestId }, waitForCallback: true, options: null).ConfigureAwait(false);
    }

    private static async Task FailAsync(ICDPSession session, string requestId)
    {
        _ = await session.SendAsync("Fetch.failRequest", new { requestId, errorReason = "BlockedByClient" }, waitForCallback: true, options: null).ConfigureAwait(false);
    }
}
