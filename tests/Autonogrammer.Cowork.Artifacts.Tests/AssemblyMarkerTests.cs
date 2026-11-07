using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Cowork.Artifacts.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Cowork.Artifacts");
    }
}
