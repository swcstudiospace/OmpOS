using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Native.Tests.Kanban;

/// <summary>A concrete runtime signal for the view tests. Shipped code has only the abstract <see cref="RuntimeSignal"/> base.</summary>
internal sealed record TestSignal : RuntimeSignal
{
    /// <summary>Creates a signal of the given kind.</summary>
    /// <param name="kind">The signal kind.</param>
    public TestSignal(RuntimeSignalKind kind)
        : base(kind)
    {
    }
}
