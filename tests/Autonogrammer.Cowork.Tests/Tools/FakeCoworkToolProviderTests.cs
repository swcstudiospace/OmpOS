using Autonogrammer.Cowork.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Tools;

/// <summary>The shared contract base against the fake, and a negative control that never asks.</summary>
public sealed class FakeCoworkToolProviderTests : CoworkProviderContractTests
{
    public FakeCoworkToolProviderTests()
        : base((broker, probe) => new FakeCoworkToolProvider(broker, probe))
    {
    }

    [Fact]
    public Task Fake_SatisfiesProviderContract() => AssertContractAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task NonAskingByProviderGate_FailsContract()
    {
        var negative = new NegativeControl();
        var exception = await Should.ThrowAsync<CoworkProviderContractViolationException>(() => negative.AssertContractAsync(TestContext.Current.CancellationToken));
        exception.FailedRules.ShouldContain("broker-not-asked-must-be-local", StringComparer.Ordinal);
        exception.Message.ShouldNotContain("{");
    }

    private sealed class NegativeControl : CoworkProviderContractTests
    {
        public NegativeControl()
            : base((_, _) => new SilentCoworkToolProvider())
        {
        }
    }
}
