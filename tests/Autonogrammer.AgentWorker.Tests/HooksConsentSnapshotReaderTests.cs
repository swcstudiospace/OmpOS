using System.Security.Claims;
using Autonogrammer.AgentWorker.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>The consent snapshot claim: what a worker accepts from the signed token and what it refuses as a whole.</summary>
public sealed class HooksConsentSnapshotReaderTests
{
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    private static ClaimsPrincipal Principal(params string[] consentClaims) =>
        new(new ClaimsIdentity([new Claim("sub", "session-1"), .. consentClaims.Select(static value => new Claim(HooksConsentSnapshotReader.ClaimType, value))], "test"));

    /// <summary>A token without the claim has no snapshot and no refusal: the connection simply carries no consent.</summary>
    [Fact]
    public void ATokenWithoutTheClaimCarriesNoSnapshot()
    {
        var read = HooksConsentSnapshotReader.Read(Principal(), 256);

        read.Snapshot.ShouldBeNull();
        read.Problem.ShouldBeNull();
    }

    /// <summary>A valid claim gives the approver, the time and every hash.</summary>
    [Fact]
    public void AValidClaimGivesTheSnapshot()
    {
        var read = HooksConsentSnapshotReader.Read(Principal(HooksTokenMinter.Snapshot(HashA, HashB)), 256);

        read.Problem.ShouldBeNull();
        var snapshot = read.Snapshot.ShouldNotBeNull();
        snapshot.Version.ShouldBe(2);
        snapshot.Approver.ShouldBe("ada@example.test");
        snapshot.ApprovedAt.ShouldBe(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        snapshot.Hashes.ToArray().ShouldBe([HashA, HashB]);
    }

    /// <summary>A snapshot that approves nothing is valid: the user saw the hooks and approved none.</summary>
    [Fact]
    public void AnEmptyListOfHashesIsValid()
    {
        var read = HooksConsentSnapshotReader.Read(Principal(HooksTokenMinter.Snapshot()), 256);

        read.Snapshot.ShouldNotBeNull().Hashes.ShouldBeEmpty();
    }

    /// <summary>Each way a claim can be wrong refuses the whole claim with a fixed reason that never repeats the claim text.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"version\":2,\"approver\":\"a\",\"hashes\":[]}")]
    [InlineData("{\"version\":1,\"approver\":\"a\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[]}")]
    [InlineData("{\"version\":3,\"approver\":\"a\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[]}")]
    [InlineData("{\"version\":2,\"approver\":\"\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[]}")]
    [InlineData("{\"version\":2,\"approver\":\"bad\\u0007name\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[]}")]
    [InlineData("{\"version\":2,\"approver\":\"a\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[\"short\"]}")]
    [InlineData("{\"version\":2,\"approver\":\"a\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"]}")]
    [InlineData("{\"version\":2,\"approver\":\"a\",\"approvedAt\":\"2026-10-27T12:00:00+00:00\",\"hashes\":[null]}")]
    public void AnInvalidClaimIsRefusedAsAWhole(string claim)
    {
        var read = HooksConsentSnapshotReader.Read(Principal(claim), 256);

        read.Snapshot.ShouldBeNull();
        var problem = read.Problem.ShouldNotBeNull();
        problem.ShouldNotContain(claim, Case.Sensitive);
    }

    /// <summary>A snapshot over the configured number of hashes is refused, and one at the limit is accepted.</summary>
    [Fact]
    public void TheNumberOfHashesIsBounded()
    {
        var hashes = Enumerable.Range(0, 3).Select(static index => index.ToString("x64", System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        HooksConsentSnapshotReader.Read(Principal(HooksTokenMinter.Snapshot(hashes)), 3).Snapshot.ShouldNotBeNull();
        var refused = HooksConsentSnapshotReader.Read(Principal(HooksTokenMinter.Snapshot(hashes)), 2);

        refused.Snapshot.ShouldBeNull();
        refused.Problem.ShouldNotBeNull();
    }

    /// <summary>Two consent claims are ambiguous, so neither is trusted.</summary>
    [Fact]
    public void TwoClaimsAreRefused()
    {
        var read = HooksConsentSnapshotReader.Read(Principal(HooksTokenMinter.Snapshot(HashA), HooksTokenMinter.Snapshot(HashB)), 256);

        read.Snapshot.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
    }

    /// <summary>A claim over 64 KiB is refused before it is parsed.</summary>
    [Fact]
    public void AnOversizedClaimIsRefused()
    {
        var read = HooksConsentSnapshotReader.Parse(HooksTokenMinter.Snapshot() + new string(' ', 70 * 1024), 256);

        read.Snapshot.ShouldBeNull();
        read.Problem.ShouldNotBeNull();
    }
}
