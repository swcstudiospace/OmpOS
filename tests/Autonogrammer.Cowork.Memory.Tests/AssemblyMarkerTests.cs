using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Memory.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Cowork.Memory.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Cowork.Memory");
    }
}
