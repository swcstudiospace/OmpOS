namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// The header names and signature format of the webhook receiver, documented in <c>docs/reference/automations.md</c>.
/// </summary>
public static class WebhookHeaders
{
    /// <summary>The prefix of every signature value: <c>sha256=</c> followed by 64 lower-case hex digits.</summary>
    public const string SignaturePrefix = "sha256=";

    /// <summary>The generic flavour's signature: HMAC-SHA256 over the timestamp, a dot and the raw body.</summary>
    public const string Signature = "X-OmpOS-Signature";

    /// <summary>The generic flavour's timestamp, in whole Unix seconds. Deliveries outside the replay window are refused.</summary>
    public const string Timestamp = "X-OmpOS-Timestamp";

    /// <summary>The generic flavour's optional delivery identifier, shown in the run summary.</summary>
    public const string Delivery = "X-OmpOS-Delivery";

    /// <summary>The GitHub flavour's signature: HMAC-SHA256 over the raw body.</summary>
    public const string GitHubSignature = "X-Hub-Signature-256";

    /// <summary>The GitHub event name, for example <c>push</c>, <c>pull_request</c> or <c>ping</c>.</summary>
    public const string GitHubEvent = "X-GitHub-Event";

    /// <summary>The GitHub delivery identifier, used to refuse a repeated delivery.</summary>
    public const string GitHubDelivery = "X-GitHub-Delivery";
}
