using System.Globalization;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Vision;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>
/// Mid-call asks. Skip never skips stage 2. Sensitive typing, submits and download release are MustBeLocal.
/// </summary>
public sealed class BrowserApprovalStage2
{
    private readonly ICoworkApprovalBroker _broker;
    private readonly ILocalPortPermit _ports;
    private readonly OriginGrants _grants;
    private readonly HashSet<string> _submitOrigins = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<CoworkGrantKey> _stored = [];

    /// <summary>Initializes stage 2.</summary>
    /// <param name="broker">The broker. Skip does not bypass it.</param>
    /// <param name="ports">The port permit. It opens only after an allow.</param>
    /// <param name="grants">The session local-origin grants.</param>
    public BrowserApprovalStage2(ICoworkApprovalBroker broker, ILocalPortPermit ports, OriginGrants grants)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(grants);
        _broker = broker;
        _ports = ports;
        _grants = grants;
    }

    /// <summary>Gets grants this stage stored. EndSession forgets them.</summary>
    public IReadOnlyCollection<CoworkGrantKey> StoredGrants => _stored;

    /// <summary>Manual asks for every click and type.</summary>
    /// <param name="manual">Whether the permission mode is Manual.</param>
    /// <returns>True when Manual.</returns>
    public static bool AsksEveryClick(bool manual) => manual;

    /// <summary>Accepts beforeunload and dismisses every other dialog.</summary>
    /// <param name="dialogType">The dialog type.</param>
    /// <returns>True only for beforeunload.</returns>
    public static bool AcceptDialog(string? dialogType) =>
        string.Equals(dialogType, "beforeunload", StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds a redirect card. Open skips it except on a dispatch turn.</summary>
    /// <param name="observed">The host-observed URL.</param>
    /// <param name="mode">The effective policy.</param>
    /// <returns>The card, or null when the mode skips it.</returns>
    public static CoworkCallPreview? RedirectCard(Uri observed, PolicyMode mode)
    {
        ArgumentNullException.ThrowIfNull(observed);
        if (mode == PolicyMode.Open)
        {
            return null;
        }

        if (OriginGrants.IsEligible(observed.Host))
        {
            var value = observed.Host + ":" + observed.Port.ToString(CultureInfo.InvariantCulture);
            return new CoworkCallPreview("Open this local origin?", UrlWithoutQuery(observed), [], new CoworkGrantKey("browser.local", value));
        }

        return new CoworkCallPreview("Open this origin?", UrlWithoutQuery(observed), [], new CoworkGrantKey("browser.origin", observed.GetLeftPart(UriPartial.Authority)));
    }

    /// <summary>Asks for a redirect even when the permission mode is Skip.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="callId">The call.</param>
    /// <param name="toolName">The tool.</param>
    /// <param name="observed">The observed URL.</param>
    /// <param name="mode">The effective policy.</param>
    /// <param name="origin">The turn origin.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>True when the hop may continue.</returns>
    public async ValueTask<bool> AskRedirectAsync(string sessionId, string callId, string toolName, Uri observed, PolicyMode mode, CoworkTurnOrigin origin, CancellationToken cancellationToken)
    {
        var card = RedirectCard(observed, mode);
        if (card is null)
        {
            return mode != PolicyMode.Strict;
        }

        if (mode == PolicyMode.Strict)
        {
            return false;
        }

        var decision = await AskAsync(sessionId, callId, toolName, card, origin, mustBeLocal: false, cancellationToken).ConfigureAwait(false);
        if (decision is not CoworkApprovalOutcome.Allowed and not CoworkApprovalOutcome.AllowedAlways)
        {
            return false;
        }

        if (card.Grant is { Scope: "browser.local" } local && TrySplit(local.Value, out var host, out var port))
        {
            _ports.Open(host, port);
            _ = _grants.TryAdd(new LocalOriginGrant(host, port), always: false);
        }

        return true;
    }

    /// <summary>Asks every sensitive type or submit with MustBeLocal. A dispatch turn with no local user is denied.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="callId">The call.</param>
    /// <param name="toolName">The tool.</param>
    /// <param name="headline">The card headline. It never contains typed text.</param>
    /// <param name="origin">The turn origin.</param>
    /// <param name="hasLocalUser">Whether a local user can answer.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>True when allowed on this machine.</returns>
    public async ValueTask<bool> AskSensitiveAsync(string sessionId, string callId, string toolName, string headline, CoworkTurnOrigin origin, bool hasLocalUser, CancellationToken cancellationToken)
    {
        if (origin == CoworkTurnOrigin.Dispatch && !hasLocalUser)
        {
            return false;
        }

        var card = new CoworkCallPreview(headline, null, [], null);
        var decision = await AskAsync(sessionId, callId, toolName, card, origin, mustBeLocal: true, cancellationToken).ConfigureAwait(false);
        return decision is CoworkApprovalOutcome.Allowed or CoworkApprovalOutcome.AllowedAlways;
    }

    /// <summary>A sensitive form asks every time. Other forms ask once per origin per session.</summary>
    /// <param name="originKey">The origin.</param>
    /// <param name="sensitive">Whether the form has a sensitive field.</param>
    /// <returns>True when a card is required.</returns>
    public bool NeedsSubmitAsk(string originKey, bool sensitive)
    {
        if (sensitive)
        {
            return true;
        }

        return _submitOrigins.Add(originKey);
    }

    /// <summary>Forgets stored approvals.</summary>
    public void Forget()
    {
        _stored.Clear();
        _submitOrigins.Clear();
    }

    /// <summary>Drops the query and fragment.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The URL without query or fragment.</returns>
    public static string UrlWithoutQuery(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var host = url.HostNameType == UriHostNameType.IPv6 ? "[" + url.Host + "]" : url.Host;
        var port = url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture);
        return url.Scheme + "://" + host + port + url.AbsolutePath;
    }

    private async ValueTask<CoworkApprovalOutcome> AskAsync(string sessionId, string callId, string toolName, CoworkCallPreview card, CoworkTurnOrigin origin, bool mustBeLocal, CancellationToken cancellationToken)
    {
        var decision = await _broker.RequestAsync(
            new CoworkApprovalRequest(sessionId, callId, toolName, card, origin, mustBeLocal),
            cancellationToken).ConfigureAwait(false);
        var (outcome, store) = BrowserLimits.NormalizeAlways(decision.Outcome, origin);
        if (store && card.Grant is { } grant)
        {
            _stored.Add(grant);
        }

        return outcome;
    }

    private static bool TrySplit(string value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var split = value.LastIndexOf(':');
        if (split <= 0 || !int.TryParse(value[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port))
        {
            return false;
        }

        host = value[..split];
        return true;
    }
}
