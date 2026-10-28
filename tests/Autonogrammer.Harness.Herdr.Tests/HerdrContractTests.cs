using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

public sealed class HerdrContractTests
{
    [Fact]
    public void ContractVersionIsOne()
    {
        HerdrContract.ContractVersion.ShouldBe(1);
    }
}
