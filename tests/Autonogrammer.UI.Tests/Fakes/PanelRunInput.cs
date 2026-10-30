using Autonogrammer.Presentation.Runs;

namespace Autonogrammer.UI.Tests.Fakes;

/// <summary>The engine side of steering: it records the steers and follow-ups a run received.</summary>
internal sealed class PanelRunInput : IRunInput
{
    /// <summary>Gets the steering messages delivered, in order.</summary>
    internal List<string> Steers { get; } = [];

    /// <summary>Gets the follow-ups started, in order.</summary>
    internal List<string> FollowUps { get; } = [];

    /// <inheritdoc />
    public int SteersHeld => 0;

    /// <inheritdoc />
    public Task SteerAsync(string text, CancellationToken cancellationToken)
    {
        Steers.Add(text);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FollowUpAsync(string text, CancellationToken cancellationToken)
    {
        FollowUps.Add(text);
        return Task.CompletedTask;
    }
}
