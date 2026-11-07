using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Harness.BrowserUse.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Harness.BrowserUse");
    }
}
