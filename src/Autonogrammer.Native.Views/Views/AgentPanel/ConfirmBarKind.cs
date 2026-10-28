namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>Which confirmation bar of <see cref="ConfirmBarView"/> an event concerns.</summary>
internal enum ConfirmBarKind
{
    /// <summary>The bar that asks whether to stop the running turn.</summary>
    Stop = 0,

    /// <summary>The bar that asks whether to close a running agent.</summary>
    Close = 1,
}
