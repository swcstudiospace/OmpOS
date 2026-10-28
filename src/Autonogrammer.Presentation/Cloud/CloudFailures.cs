using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Presentation.Cloud;

/// <summary>Turns a failed host call into what the models show. Only the message of a <see cref="CloudHostException"/> is shown; any other exception reads as the fallback, so no unexpected text reaches the screen.</summary>
internal static class CloudFailures
{
    private const int MaxMessageChars = 300;

    /// <summary>Gets why the call failed: the kind of a <see cref="CloudHostException"/>, otherwise <see cref="CloudFailureKind.Unavailable"/>.</summary>
    /// <param name="fault">The exception.</param>
    /// <returns>The kind.</returns>
    internal static CloudFailureKind KindOf(Exception fault) => fault is CloudHostException host ? host.Kind : CloudFailureKind.Unavailable;

    /// <summary>Gets the plain, capped message of a <see cref="CloudHostException"/>, or <paramref name="fallback"/> for anything else or an empty message.</summary>
    /// <param name="fault">The exception.</param>
    /// <param name="fallback">The text for an unexpected failure.</param>
    /// <returns>The text to show.</returns>
    internal static string MessageOf(Exception fault, string fallback)
    {
        if (fault is CloudHostException host)
        {
            var plain = PanelText.Plain(host.Message, MaxMessageChars);
            if (plain.Length > 0)
            {
                return plain;
            }
        }

        return fallback;
    }

    /// <summary>Gets whether an exception is one a model turns into a message: everything except a cancellation and an out-of-memory failure.</summary>
    /// <param name="fault">The exception.</param>
    /// <returns>True when the model handles it.</returns>
    internal static bool IsHandled(Exception fault) => fault is not (OperationCanceledException or OutOfMemoryException);
}
