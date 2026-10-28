namespace Autonogrammer.Native.Views;

/// <summary>The arguments of <see cref="ComposerView.WorkFaulted"/>: the fault of a composer action.</summary>
internal sealed class ComposerWorkFaultedEventArgs : EventArgs
{
    /// <summary>Creates the arguments for a fault.</summary>
    /// <param name="exception">The fault the composer action raised.</param>
    public ComposerWorkFaultedEventArgs(AggregateException exception)
    {
        Exception = exception;
    }

    /// <summary>Gets the fault the composer action raised.</summary>
    public AggregateException Exception { get; }
}
