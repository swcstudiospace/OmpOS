using Autonogrammer.Presentation.Runs;

namespace Autonogrammer.Native.Engine;

/// <summary>
/// The production default run input while no engine is connected in this build. It delivers nothing: every steer and
/// follow-up faults, so <see cref="RunInputScheduler"/> keeps a follow-up queued and the composer keeps the draft.
/// </summary>
public sealed class UnconnectedRunInput : IRunInput
{
    /// <summary>The fault message shown to the user while no run input is connected.</summary>
    public const string UnavailableText = "No run input is connected in this build.";

    /// <inheritdoc/>
    public int SteersHeld => 0;

    /// <inheritdoc/>
    public Task SteerAsync(string text, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException(UnavailableText));

    /// <inheritdoc/>
    public Task FollowUpAsync(string text, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException(UnavailableText));
}
