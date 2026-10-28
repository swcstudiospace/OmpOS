using System.Text;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Decides what to do with one webhook delivery to one automation: it checks the signature in constant time, the timestamp (generic
/// flavour) against the replay window, repeats against a replay cache, the body size, and the trigger's event, action and branch
/// filters. The decision for an unauthorized delivery carries a stable reason for the server log; the receiver never sends it to the caller.
/// </summary>
/// <remarks>
/// Order of checks: body size, signature (the flavour follows the signature header that is present), timestamp, replay, then the filters.
/// Nothing is recorded in the replay cache before the signature verified, so an unauthenticated caller cannot fill it.
/// </remarks>
public sealed class WebhookTrigger
{
    private static readonly TimeSpan GitHubReplayRetention = TimeSpan.FromHours(1);

    private readonly TimeProvider _clock;
    private readonly WebhookTriggerOptions _options;
    private readonly WebhookReplayCache _replays = new();

    /// <summary>Initializes a new instance of the <see cref="WebhookTrigger"/> class.</summary>
    /// <param name="clock">The clock that judges timestamps and expires replay entries.</param>
    /// <param name="options">The limits, or null for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException">The body cap is outside 1 to 262144 bytes or the replay window is not positive.</exception>
    public WebhookTrigger(TimeProvider clock, WebhookTriggerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _options = options ?? new WebhookTriggerOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.MaxBodyBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_options.MaxBodyBytes, WebhookTriggerOptions.MaxBodyBytesCeiling);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.ReplayWindow, TimeSpan.Zero);
    }

    /// <summary>Gets the largest body accepted, in bytes.</summary>
    public int MaxBodyBytes => _options.MaxBodyBytes;

    /// <summary>Evaluates a delivery.</summary>
    /// <param name="automation">The automation the URL named, or null when none exists.</param>
    /// <param name="secret">The automation's webhook secret, or null when it has none.</param>
    /// <param name="request">The delivery.</param>
    /// <returns>The decision.</returns>
    public WebhookDecision Evaluate(UserAutomation? automation, string? secret, WebhookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Body.Length > _options.MaxBodyBytes)
        {
            return new WebhookDecision(WebhookOutcome.TooLarge, "body_too_large");
        }

        if (automation is null || !automation.HasWebhook || string.IsNullOrEmpty(secret))
        {
            return new WebhookDecision(WebhookOutcome.Unauthorized, "unknown_automation");
        }

        var flavour = request.TryGetHeader(WebhookHeaders.GitHubSignature, out _) ? WebhookFlavour.GitHub : WebhookFlavour.Generic;
        var triggers = automation.Triggers.Where(trigger => trigger.Kind == AutomationTriggerKind.Webhook && trigger.Flavour == flavour).ToArray();
        if (triggers.Length == 0)
        {
            return new WebhookDecision(WebhookOutcome.Unauthorized, "flavour_not_configured");
        }

        var authentic = flavour == WebhookFlavour.GitHub
            ? VerifyGitHub(secret, request, out var key, out var retention, out var failure)
            : VerifyGeneric(secret, request, out key, out retention, out failure);
        if (!authentic)
        {
            return new WebhookDecision(WebhookOutcome.Unauthorized, failure);
        }

        if (!_replays.TryAdd(key, _clock.GetUtcNow(), retention))
        {
            return new WebhookDecision(WebhookOutcome.Replay, "replayed_delivery");
        }

        if (!automation.Enabled)
        {
            return new WebhookDecision(WebhookOutcome.Ignored, "automation_disabled");
        }

        return flavour == WebhookFlavour.GitHub ? FilterGitHub(triggers, request) : AcceptGeneric(request);
    }

    private bool VerifyGeneric(string secret, WebhookRequest request, out string key, out TimeSpan retention, out string failure)
    {
        key = string.Empty;
        retention = _options.ReplayWindow * 2;
        failure = "missing_signature";
        if (!request.TryGetHeader(WebhookHeaders.Signature, out var signature) || !request.TryGetHeader(WebhookHeaders.Timestamp, out var timestamp))
        {
            return false;
        }

        if (!WebhookSignature.Verify(secret, WebhookSignature.GenericSignedBytes(timestamp, request.Body.Span), signature))
        {
            failure = "bad_signature";
            return false;
        }

        if (!WebhookSignature.TryParseTimestamp(timestamp, out var sentAt)
            || (_clock.GetUtcNow() - sentAt).Duration() > _options.ReplayWindow)
        {
            failure = "stale_timestamp";
            return false;
        }

        key = "g:" + signature;
        failure = string.Empty;
        return true;
    }

    private static bool VerifyGitHub(string secret, WebhookRequest request, out string key, out TimeSpan retention, out string failure)
    {
        key = string.Empty;
        retention = GitHubReplayRetention;
        failure = "missing_signature";
        if (!request.TryGetHeader(WebhookHeaders.GitHubSignature, out var signature))
        {
            return false;
        }

        if (!WebhookSignature.Verify(secret, request.Body.Span, signature))
        {
            failure = "bad_signature";
            return false;
        }

        key = request.TryGetHeader(WebhookHeaders.GitHubDelivery, out var delivery) && delivery.Length <= 100
            ? "h:" + delivery
            : "h:" + signature;
        failure = string.Empty;
        return true;
    }

    private static WebhookDecision AcceptGeneric(WebhookRequest request)
    {
        var summary = request.TryGetHeader(WebhookHeaders.Delivery, out var delivery) && delivery.Length <= 100
            ? "Webhook delivery " + GitHubEventReader.Line(delivery, 100)
            : "Webhook delivery";
        var text = Encoding.UTF8.GetString(request.Body.Span);
        var context = new AutomationTriggerContext
        {
            Source = AutomationRunSource.Webhook,
            Summary = summary,
            Payload = text.Length <= 4_000 ? text : text[..4_000],
        };
        return new WebhookDecision(WebhookOutcome.Accepted, "accepted", context);
    }

    private static WebhookDecision FilterGitHub(AutomationTriggerSpec[] triggers, WebhookRequest request)
    {
        if (!request.TryGetHeader(WebhookHeaders.GitHubEvent, out var eventName))
        {
            return new WebhookDecision(WebhookOutcome.Ignored, "missing_event");
        }

        if (string.Equals(eventName, "ping", StringComparison.Ordinal))
        {
            return new WebhookDecision(WebhookOutcome.Ping, "ping");
        }

        if (eventName is not ("push" or "pull_request"))
        {
            return new WebhookDecision(WebhookOutcome.Ignored, "event_filter");
        }

        if (!GitHubEventReader.TryRead(eventName, request.Body, out var info, out var reason))
        {
            return new WebhookDecision(WebhookOutcome.Ignored, reason);
        }

        if (info.IsForkPullRequest && !string.Equals(info.Action, "merged", StringComparison.Ordinal))
        {
            return new WebhookDecision(WebhookOutcome.Ignored, "fork_pull_request");
        }

        foreach (var trigger in triggers)
        {
            var eventAllowed = trigger.Events.Count == 0 || trigger.Events.Contains(info.Event, StringComparer.Ordinal);
            var actionAllowed = info.Action.Length == 0 || trigger.Actions.Count == 0 || trigger.Actions.Contains(info.Action, StringComparer.Ordinal);
            if (eventAllowed && actionAllowed && BranchGlob.MatchesAnyOrNoFilter(trigger.Branches, info.Branch))
            {
                var context = new AutomationTriggerContext
                {
                    Source = AutomationRunSource.Webhook,
                    Summary = info.Summary,
                    Payload = info.Payload,
                };
                return new WebhookDecision(WebhookOutcome.Accepted, "accepted", context);
            }
        }

        return new WebhookDecision(WebhookOutcome.Ignored, "filter_mismatch");
    }
}
