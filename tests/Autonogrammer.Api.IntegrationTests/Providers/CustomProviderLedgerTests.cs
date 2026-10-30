using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Contracts.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>The ledger of custom endpoints: each caller lists, replaces and removes only their own.</summary>
public sealed class CustomProviderLedgerTests
{
    private static CustomProviderRecord Record(string owner, string id, string url = "https://llm.example.com/v1") =>
        new(owner, id, id, CustomProviderProtocol.OpenAi, url, ["m"], KeyEnvName: null);

    [Fact]
    public void AnIdBelongsToTheFirstCallerThatSetsIt()
    {
        var ledger = new CustomProviderLedger();

        ledger.TrySet(Record("alice", "mine")).ShouldBeTrue();
        ledger.TrySet(Record("bob", "mine")).ShouldBeFalse();
        ledger.TrySet(Record("alice", "mine", "https://other.example.com/v1")).ShouldBeTrue();

        ledger.Find("mine")!.BaseUrl.ShouldBe("https://other.example.com/v1");
        ledger.Find("mine")!.Owner.ShouldBe("alice");
    }

    [Fact]
    public void AListHoldsOnlyTheRecordsOfItsCallerOrderedById()
    {
        var ledger = new CustomProviderLedger();
        _ = ledger.TrySet(Record("alice", "zeta"));
        _ = ledger.TrySet(Record("alice", "alpha"));
        _ = ledger.TrySet(Record("bob", "beta"));

        ledger.ListFor("alice").Select(static record => record.Id).ShouldBe(["alpha", "zeta"]);
        ledger.ListFor("bob").Select(static record => record.Id).ShouldBe(["beta"]);
        ledger.ListFor("carol").ShouldBeEmpty();
    }

    [Fact]
    public void OnlyTheOwnerRemovesARecord()
    {
        var ledger = new CustomProviderLedger();
        _ = ledger.TrySet(Record("alice", "mine"));

        ledger.TryRemove("bob", "mine").ShouldBeFalse();
        ledger.Find("mine").ShouldNotBeNull();
        ledger.TryRemove("alice", "mine").ShouldBeTrue();
        ledger.Find("mine").ShouldBeNull();
        ledger.TryRemove("alice", "mine").ShouldBeFalse();
    }
}
