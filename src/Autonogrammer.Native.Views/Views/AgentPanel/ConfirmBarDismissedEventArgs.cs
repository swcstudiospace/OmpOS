namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>The arguments of <see cref="ConfirmBarView.Dismissed"/>: which confirmation bar was dismissed.</summary>
internal sealed class ConfirmBarDismissedEventArgs : EventArgs
{
    /// <summary>Creates the arguments for one dismissed bar.</summary>
    /// <param name="kind">The bar that was dismissed.</param>
    public ConfirmBarDismissedEventArgs(ConfirmBarKind kind)
    {
        Kind = kind;
    }

    /// <summary>Gets the bar that was dismissed.</summary>
    public ConfirmBarKind Kind { get; }
}
