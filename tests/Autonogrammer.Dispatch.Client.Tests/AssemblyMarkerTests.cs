using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Client.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Dispatch.Client.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Dispatch.Client");
    }
}
