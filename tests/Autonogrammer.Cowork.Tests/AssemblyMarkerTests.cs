using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Cowork.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Cowork");
    }
}
