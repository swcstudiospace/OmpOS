using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Abstractions;

/// <summary>
/// Smoke test proving the L1 Cowork.Abstractions assembly loads under test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Cowork.Abstractions.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Cowork.Abstractions");
    }
}
