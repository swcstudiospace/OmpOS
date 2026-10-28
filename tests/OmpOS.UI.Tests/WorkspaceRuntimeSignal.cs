using OmpOS.Presentation.Kanban;

namespace OmpOS.UI.Tests;

/// <summary>A runtime signal of a chosen kind, standing in for the concrete signal records the runtime contracts will bring.</summary>
internal sealed record WorkspaceRuntimeSignal : RuntimeSignal
{
    /// <summary>Creates the signal.</summary>
    /// <param name="kind">The runtime fact the signal reports.</param>
    internal WorkspaceRuntimeSignal(RuntimeSignalKind kind)
        : base(kind)
    {
    }
}
