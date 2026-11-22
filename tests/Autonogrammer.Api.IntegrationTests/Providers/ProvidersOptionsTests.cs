using Autonogrammer.Api.Services.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>Verifies the Settings probe budget at the backend: one probe step defaults to 5 seconds (NFR-P1) so a test flow completes or cancels within the 5-second step.</summary>
public sealed class ProvidersOptionsTests
{
    /// <summary>The default probe timeout is 5 seconds.</summary>
    [Fact]
    public void DefaultTestTimeout_IsFiveSeconds()
    {
        new ProvidersOptions().TestTimeoutSeconds.ShouldBe(5);
    }
}
