namespace OmpOS.Harness.Plugins;

/// <summary>
/// Builds the reason persisted with a quarantine. The exception's type and message are kept, so the record shows why
/// the plugin was stopped rather than hiding the failure.
/// </summary>
internal static class FaultReason
{
    /// <summary>
    /// Describes a fault.
    /// </summary>
    /// <param name="subject">What failed, such as the hook identifier.</param>
    /// <param name="exception">The exception the host caught.</param>
    /// <returns>The reason text.</returns>
    public static string Describe(string subject, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return $"{subject} failed: {exception.GetType().FullName}: {exception.Message}";
    }
}
