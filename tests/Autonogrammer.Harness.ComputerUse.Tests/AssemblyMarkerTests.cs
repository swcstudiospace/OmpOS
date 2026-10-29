using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Harness.ComputerUse.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Harness.ComputerUse");
    }
}
