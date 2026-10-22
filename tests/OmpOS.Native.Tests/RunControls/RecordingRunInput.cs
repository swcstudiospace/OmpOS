using OmpOS.Presentation.Runs;

namespace OmpOS.Native.Tests.RunControls;

/// <summary>A run input that records every steer and follow-up it delivers, and faults each steer when <see cref="SteerFault"/> is set.</summary>
internal sealed class RecordingRunInput : IRunInput
{
    /// <summary>Gets the steering messages the run accepted, in order.</summary>
    public List<string> Steers { get; } = [];

    /// <summary>Gets the follow-ups the run started, in order.</summary>
    public List<string> FollowUps { get; } = [];

    /// <summary>Gets or sets the fault every steer raises instead of being delivered.</summary>
    public Exception? SteerFault { get; set; }

    /// <inheritdoc/>
    public Task SteerAsync(string text, CancellationToken cancellationToken)
    {
        if (SteerFault is not null)
        {
            return Task.FromException(SteerFault);
        }

        Steers.Add(text);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task FollowUpAsync(string text, CancellationToken cancellationToken)
    {
        FollowUps.Add(text);
        return Task.CompletedTask;
    }
}
