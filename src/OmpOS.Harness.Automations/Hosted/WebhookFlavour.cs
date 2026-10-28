namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Which sender a webhook trigger accepts. The flavour decides the signature headers and whether the payload is interpreted.
/// </summary>
public enum WebhookFlavour
{
    /// <summary>OmpOS's own envelope: <c>X-OmpOS-Signature</c> over <c>timestamp.body</c> and <c>X-OmpOS-Timestamp</c>. Any JSON or text body starts a run.</summary>
    Generic,

    /// <summary>GitHub: <c>X-Hub-Signature-256</c> over the body and <c>X-GitHub-Event</c>. Events and branches can be filtered.</summary>
    GitHub,
}
