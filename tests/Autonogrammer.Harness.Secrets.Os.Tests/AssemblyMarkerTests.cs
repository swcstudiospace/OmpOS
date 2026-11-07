using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Os.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Harness.Secrets.Os.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Harness.Secrets.Os");
    }
}
