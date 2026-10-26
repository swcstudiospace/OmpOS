using OmpOS.Presentation.Runs;

namespace OmpOS.Presentation.Tests.AgentPanel;

/// <summary>A run input that records what it was given and can hold steers, as the ACP path does.</summary>
internal sealed class ScriptedRunInput : IRunInput
{
    /// <summary>Gets the steering messages delivered, in order.</summary>
    internal List<string> Steers { get; } = [];

    /// <summary>Gets the follow-ups started, in order.</summary>
    internal List<string> FollowUps { get; } = [];

    /// <summary>Gets or sets whether a steer is held until its turn starts, so <see cref="SteersHeld"/> grows.</summary>
    internal bool HoldSteers { get; set; }

    /// <inheritdoc />
    public int SteersHeld { get; set; }

    /// <inheritdoc />
    public Task SteerAsync(string text, CancellationToken cancellationToken)
    {
        Steers.Add(text);
        if (HoldSteers)
        {
            SteersHeld++;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task FollowUpAsync(string text, CancellationToken cancellationToken)
    {
        FollowUps.Add(text);
        return Task.CompletedTask;
    }
}
