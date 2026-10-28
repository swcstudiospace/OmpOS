using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>A concrete runtime signal for tests. Shipped code has only the abstract <see cref="RuntimeSignal"/> base.</summary>
internal sealed record TestRuntimeSignal : RuntimeSignal
{
    /// <summary>Creates a test signal of the given kind.</summary>
    /// <param name="kind">The signal kind; it may be a value outside the enum to exercise refusal.</param>
    public TestRuntimeSignal(RuntimeSignalKind kind)
        : base(kind)
    {
    }
}
