using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The terminal result of one command as the handler or the router reports it.</summary>
/// <param name="State">The terminal state: completed, failed or cancelled.</param>
/// <param name="Reason">The closed failure reason when the state is failed or cancelled; null when completed.</param>
/// <param name="Text">Redacted result text, when any.</param>
public readonly record struct DispatchOutcome(DispatchCommandState State, DispatchFailureReason? Reason, string? Text)
{
    /// <summary>Builds a completed outcome.</summary>
    /// <param name="text">Optional result text.</param>
    /// <returns>The outcome.</returns>
    public static DispatchOutcome Completed(string? text = null) => new(DispatchCommandState.Completed, null, text);

    /// <summary>Builds a failed outcome.</summary>
    /// <param name="reason">The closed reason.</param>
    /// <returns>The outcome.</returns>
    public static DispatchOutcome Failed(DispatchFailureReason reason) => new(DispatchCommandState.Failed, reason, null);
}
