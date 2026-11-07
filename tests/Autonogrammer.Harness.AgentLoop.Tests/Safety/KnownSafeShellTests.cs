using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>The public known-safe facade reads the internal list without InternalsVisibleTo on Cowork.</summary>
public sealed class KnownSafeShellTests
{
    [Fact]
    public void AShortReadOnlyCommandIsKnownSafeWhenArgumentsAreAllowed()
    {
        KnownSafeShell.IsKnownSafe("ls notes.txt", static argument => string.Equals(argument, "notes.txt", StringComparison.Ordinal)).ShouldBeTrue();
        KnownSafeShell.IsKnownSafe("pwd", static _ => false).ShouldBeTrue();
    }

    [Fact]
    public void AnArgumentOutsideTheBoundaryIsNotKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("ls /elsewhere", static _ => false).ShouldBeFalse();
        KnownSafeShell.IsKnownSafe("rm notes.txt", static _ => true).ShouldBeFalse();
    }
}
