using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Desktop.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Dispatch.Desktop.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Dispatch.Desktop");
    }
}
