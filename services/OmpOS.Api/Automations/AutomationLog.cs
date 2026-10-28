namespace OmpOS.Api.Automations;

/// <summary>
/// Log messages of the webhook receiver. They carry the automation identifier (part of the URL) and a stable reason code; they never carry a
/// header, a signature, a secret or any part of a body.
/// </summary>
internal static class AutomationLog
{
    private static readonly Action<ILogger, string, string, Exception?> Rejected =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(1, "WebhookRejected"), "Webhook delivery for automation {AutomationId} was not accepted: {Reason}.");

    private static readonly Action<ILogger, string, string, Exception?> Accepted =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2, "WebhookAccepted"), "Webhook delivery for automation {AutomationId} started run {RunId}.");

    private static readonly Action<ILogger, string, string, Exception?> Refused =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(3, "WebhookRunRefused"), "Webhook delivery for automation {AutomationId} did not start a run: {Reason}.");

    /// <summary>Logs a delivery that was not accepted.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="automationId">The automation identifier from the URL.</param>
    /// <param name="reason">The stable reason code.</param>
    public static void WebhookRejected(ILogger logger, string automationId, string reason) => Rejected(logger, SafeId(automationId), reason, null);

    /// <summary>Logs a delivery that started a run.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="runId">The run identifier.</param>
    public static void WebhookAccepted(ILogger logger, string automationId, string runId) => Accepted(logger, SafeId(automationId), runId, null);

    /// <summary>Logs an authentic delivery whose run a guard refused.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="automationId">The automation identifier.</param>
    /// <param name="reason">The guard.</param>
    public static void WebhookRunRefused(ILogger logger, string automationId, string reason) => Refused(logger, SafeId(automationId), reason, null);

    private static string SafeId(string id) => id.Length <= 64 && id.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_') ? id : "(invalid)";
}
