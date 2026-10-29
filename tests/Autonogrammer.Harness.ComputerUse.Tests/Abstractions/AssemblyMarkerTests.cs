using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Abstractions;

/// <summary>
/// Smoke test proving the L1 Harness.ComputerUse.Abstractions assembly loads under test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Harness.ComputerUse.Abstractions.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Harness.ComputerUse.Abstractions");
    }
}
