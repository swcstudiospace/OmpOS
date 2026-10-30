using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>
/// The unmapped-signal exception is thrown by the status map, so its behaviour is tested through the map rather than by constructing it.
/// </summary>
public sealed class UnmappedRuntimeSignalExceptionTests
{
    /// <summary>An unmapped signal kind is refused with the kind carried and the kind parameter named.</summary>
    [Fact]
    public void AnUnmappedKindIsRefusedNamingTheKindParameter()
    {
        var error = Should.Throw<UnmappedRuntimeSignalException>(() => RuntimeStatusMap.StatusOf((RuntimeSignalKind)999));

        error.ParamName.ShouldBe("kind");
        error.Kind.ShouldBe((RuntimeSignalKind?)(RuntimeSignalKind)999);
    }
}
