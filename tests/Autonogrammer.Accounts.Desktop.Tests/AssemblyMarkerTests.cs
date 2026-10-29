using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Desktop.Tests;

/// <summary>
/// Smoke test proving the test project runs at least one test.
/// </summary>
public sealed class AssemblyMarkerTests
{
    [Fact]
    public void AssemblyMarkerResolvesToItsOwnAssembly()
    {
        typeof(Autonogrammer.Accounts.Desktop.AssemblyMarker).Assembly.GetName().Name.ShouldBe("Autonogrammer.Accounts.Desktop");
    }
}
