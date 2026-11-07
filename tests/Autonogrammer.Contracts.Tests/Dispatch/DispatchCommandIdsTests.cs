using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>Command identifiers: the 26-character ULID shape of D-17, sorted by creation time, and distinct within one millisecond.</summary>
public sealed class DispatchCommandIdsTests
{
    private const string Crockford = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>An identifier is 26 Crockford base32 characters.</summary>
    [Fact]
    public void AnIdentifierIsTwentySixCrockfordCharacters()
    {
        var id = DispatchCommandIds.New(new FixedClock(DateTimeOffset.UnixEpoch.AddMilliseconds(1_700_000_000_000)));

        id.Length.ShouldBe(26);
        id.All(character => Crockford.Contains(character, StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>An identifier made later sorts after one made earlier, because the first ten characters carry the millisecond clock.</summary>
    [Fact]
    public void AnIdentifierMadeLaterSortsAfterAnEarlierOne()
    {
        var earlier = DispatchCommandIds.New(new FixedClock(DateTimeOffset.UnixEpoch.AddMilliseconds(1_000)));
        var later = DispatchCommandIds.New(new FixedClock(DateTimeOffset.UnixEpoch.AddMilliseconds(2_000)));

        string.CompareOrdinal(earlier, later).ShouldBeLessThan(0);
    }

    /// <summary>Identifiers made in one millisecond differ, because the last sixteen characters are random.</summary>
    [Fact]
    public void IdentifiersMadeInOneMillisecondDiffer()
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch.AddMilliseconds(5_000));

        var ids = Enumerable.Range(0, 200).Select(_ => DispatchCommandIds.New(clock)).ToHashSet(StringComparer.Ordinal);

        ids.Count.ShouldBe(200);
    }

    /// <summary>A clock the test sets.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
