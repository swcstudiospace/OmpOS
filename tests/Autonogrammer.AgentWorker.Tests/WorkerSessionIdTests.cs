using Autonogrammer.AgentWorker.Security;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>The session subject names a directory, so only a single safe path segment is accepted.</summary>
public sealed class WorkerSessionIdTests
{
    /// <summary>Plain ids, ids with inner punctuation and the 64 character limit are safe.</summary>
    /// <param name="value">The subject.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("Session-1")]
    [InlineData("a.b_c-d")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]
    public void SafeSubjectsAreAccepted(string value) => WorkerSessionIds.IsSafe(value).ShouldBeTrue();

    /// <summary>Separators, leading punctuation, spaces, non-ASCII, blanks and anything over 64 characters are refused.</summary>
    /// <param name="value">The subject.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("-a")]
    [InlineData("_a")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a b")]
    [InlineData("a\u00e9")]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]
    public void UnsafeSubjectsAreRefused(string value) => WorkerSessionIds.IsSafe(value).ShouldBeFalse();

    /// <summary>A missing subject is refused.</summary>
    [Fact]
    public void NullIsRefused() => WorkerSessionIds.IsSafe(null).ShouldBeFalse();
}
