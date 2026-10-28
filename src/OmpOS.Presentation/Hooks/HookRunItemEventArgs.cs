namespace OmpOS.Presentation.Hooks;

/// <summary>Event data of <see cref="IHooksPageSource.RunRecorded"/>.</summary>
public sealed class HookRunItemEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="HookRunItemEventArgs"/> class.</summary>
    /// <param name="run">The run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    public HookRunItemEventArgs(HookRunItem run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Run = run;
    }

    /// <summary>Gets the run.</summary>
    public HookRunItem Run { get; }
}
