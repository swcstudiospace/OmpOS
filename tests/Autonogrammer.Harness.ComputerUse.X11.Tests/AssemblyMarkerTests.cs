using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.X11.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Harness.ComputerUse.X11.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Harness.ComputerUse.X11");
    }
}
