namespace OmpOS.Web.Demo;

/// <summary>Timing of the scripted Demo. All delays run on the injected <see cref="TimeProvider"/>, so tests step them without sleeping.</summary>
internal sealed class DemoOptions
{
    /// <summary>Gets the delay between two chunks of streamed text (the typing cadence).</summary>
    public TimeSpan TypingDelay { get; init; } = TimeSpan.FromMilliseconds(35);

    /// <summary>Gets the pause before a tool call or a diff appears.</summary>
    public TimeSpan StepDelay { get; init; } = TimeSpan.FromMilliseconds(700);

    /// <summary>Gets a value indicating whether the scripted run of the "running" session starts when the session is first read. Tests set it to false to control the clock.</summary>
    public bool AutoStartRunningSession { get; init; } = true;
}
