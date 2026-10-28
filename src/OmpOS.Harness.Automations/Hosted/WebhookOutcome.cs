namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// What the webhook trigger decided about one delivery.
/// </summary>
public enum WebhookOutcome
{
    /// <summary>The delivery is authentic, fresh and matches a trigger: start a run.</summary>
    Accepted,

    /// <summary>The delivery failed authentication: unknown or disabled automation, missing or wrong signature, stale timestamp. The receiver answers all of them alike.</summary>
    Unauthorized,

    /// <summary>The body is larger than the cap.</summary>
    TooLarge,

    /// <summary>The delivery is authentic but repeats an earlier one inside the replay window.</summary>
    Replay,

    /// <summary>The delivery is authentic but no trigger wants it, for example another event or branch.</summary>
    Ignored,

    /// <summary>The delivery is a GitHub <c>ping</c>: the signature is right and nothing runs.</summary>
    Ping,
}
