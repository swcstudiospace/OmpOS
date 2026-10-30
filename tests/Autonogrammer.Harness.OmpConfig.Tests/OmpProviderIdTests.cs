using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpProviderIdTests
{
    [Theory]
    [InlineData("openai")]
    [InlineData("openai-codex")]
    [InlineData("a")]
    [InlineData("0lab")]
    [InlineData("my.provider_1-x")]
    public void AcceptsLowerCaseSlugs(string id)
    {
        OmpProviderId.IsValid(id).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-lead")]
    [InlineData(".lead")]
    [InlineData("_lead")]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("tab\there")]
    [InlineData("slash/er")]
    [InlineData("colon:x")]
    [InlineData("quote\"x")]
    [InlineData("trailing-newline\n")]
    [InlineData("ünïcode")]
    public void RejectsEverythingElse(string id)
    {
        OmpProviderId.IsValid(id).ShouldBeFalse();
    }

    [Fact]
    public void RejectsNull()
    {
        OmpProviderId.IsValid(null).ShouldBeFalse();
    }

    [Fact]
    public void AcceptsExactlySixtyFourCharactersAndNoMore()
    {
        OmpProviderId.IsValid(new string('a', OmpProviderId.MaxLength)).ShouldBeTrue();
        OmpProviderId.IsValid(new string('a', OmpProviderId.MaxLength + 1)).ShouldBeFalse();
    }
}
