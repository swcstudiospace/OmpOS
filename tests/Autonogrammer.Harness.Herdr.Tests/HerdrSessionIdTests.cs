using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

public sealed class HerdrSessionIdTests
{
    [Theory]
    [InlineData("alpha")]
    [InlineData("A1")]
    [InlineData("omp-session_1.2")]
    public void AcceptsSafePathSegments(string value)
    {
        new HerdrSessionId(value).Value.ShouldBe(value);
        HerdrSessionId.IsValid(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData(".hidden")]
    [InlineData("-leading")]
    [InlineData("has space")]
    [InlineData("with:colon")]
    public void RejectsValuesThatAreNotSafePathSegments(string value)
    {
        HerdrSessionId.IsValid(value).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => new HerdrSessionId(value));
    }

    [Fact]
    public void AcceptsExactlyTheMaximumLengthAndRejectsOneMore()
    {
        var atLimit = new string('a', HerdrSessionId.MaxLength);
        var overLimit = new string('a', HerdrSessionId.MaxLength + 1);

        HerdrSessionId.IsValid(atLimit).ShouldBeTrue();
        HerdrSessionId.IsValid(overLimit).ShouldBeFalse();
    }

    [Fact]
    public void NullIsNotAValidIdentifier()
    {
        HerdrSessionId.IsValid(null).ShouldBeFalse();
    }
}
