using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpDefaultModelTests
{
    private const string Listing =
        """{"models":[{"provider":"openai","id":"gpt-4o-mini","selector":"openai/gpt-4o-mini"},{"provider":"acme","id":"small","selector":"acme/small"}]}""";

    [Fact]
    public async Task ReadsTheWholeRoleMapAndTheDefaultSelector()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetRoles("""{"smol":"acme/small","default":"openai/gpt-4o-mini","slow":"openai/gpt-4o-mini"}""");
        var model = new OmpDefaultModel(fake.Options());

        var roles = await model.GetAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        roles.WasRead.ShouldBeTrue();
        roles.DefaultSelector.ShouldBe("openai/gpt-4o-mini");
        roles.Roles.Keys.ShouldBe(["smol", "default", "slow"]);
        roles.Roles["smol"].ShouldBe("acme/small");
        fake.Calls().Single().Arguments.ShouldBe(["config", "get", "modelRoles"]);
    }

    [Fact]
    public async Task NoRolesIsAnEmptyMapWithNoDefault()
    {
        using var fake = new FakeOmpEnvironment();
        var model = new OmpDefaultModel(fake.Options());

        var roles = await model.GetAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        roles.WasRead.ShouldBeTrue();
        roles.Roles.ShouldBeEmpty();
        roles.DefaultSelector.ShouldBeNull();
    }

    [Fact]
    public async Task AMissingOmpIsAProblemNotAnEmptyMap()
    {
        using var fake = new FakeOmpEnvironment();
        var model = new OmpDefaultModel(fake.OptionsWithoutOmp());

        var roles = await model.GetAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        roles.WasRead.ShouldBeFalse();
        roles.Problem.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[\"a\"]")]
    [InlineData("{\"default\": 5}")]
    public async Task UnreadableRolesAreAProblem(string output)
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetRoles(output);
        var model = new OmpDefaultModel(fake.Options());

        var roles = await model.GetAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        roles.WasRead.ShouldBeFalse();
    }

    [Fact]
    public async Task SettingTheDefaultWritesTheWholeMapSoTheOtherRolesSurvive()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(Listing);
        fake.SetRoles("""{"smol":"acme/small","default":"acme/small","slow":"openai/gpt-4o-mini","plan":"openai/gpt-4o-mini"}""");
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync("openai/gpt-4o-mini", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.Set);
        using var stored = JsonDocument.Parse(fake.ReadRoles()!);
        var map = stored.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.Ordinal);
        map.Count.ShouldBe(4);
        map["smol"].ShouldBe("acme/small");
        map["default"].ShouldBe("openai/gpt-4o-mini");
        map["slow"].ShouldBe("openai/gpt-4o-mini");
        map["plan"].ShouldBe("openai/gpt-4o-mini");
        var write = fake.Calls().Single(call => call.Arguments.Contains("set", StringComparer.Ordinal));
        write.Arguments.Count.ShouldBe(4);
        write.Arguments.Take(3).ShouldBe(["config", "set", "modelRoles"]);
    }

    [Fact]
    public async Task SettingTheDefaultAddsTheRoleWhenNoRolesExist()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(Listing);
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync("acme/small", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.IsSet.ShouldBeTrue();
        fake.ReadRoles().ShouldBe("""{"default":"acme/small"}""");
    }

    [Fact]
    public async Task ASelectorOutsideACompleteListingIsUnknownAndNothingIsWritten()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(Listing);
        fake.SetRoles("""{"default":"acme/small"}""");
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync("openai/not-a-model", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.UnknownModel);
        fake.ReadRoles().ShouldBe("""{"default":"acme/small"}""");
        fake.Calls().ShouldNotContain(call => call.Arguments.Contains("set", StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnEmptyCredentialGatedListingMakesEverySelectorUnknown()
    {
        using var fake = new FakeOmpEnvironment();
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync("openai/gpt-4o-mini", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.UnknownModel);
        result.Message.ShouldContain("credential", Case.Sensitive);
    }

    [Fact]
    public async Task AnIncompleteListingDoesNotBlockTheWrite()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("""{"models":[]}""");
        fake.SetModelsStandardError(FakeOmpEnvironment.ValidationWarningLine + "\n");
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync("acme/custom-model", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.Set);
        fake.ReadRoles().ShouldBe("""{"default":"acme/custom-model"}""");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-slash")]
    [InlineData("/model")]
    [InlineData("provider/")]
    [InlineData("Upper/model")]
    [InlineData("provider/model with space")]
    [InlineData("provider/model\nnewline")]
    [InlineData("provider/model\u0007bell")]
    [InlineData("bad provider/model")]
    public async Task AnInvalidSelectorIsRejectedBeforeAnyProcessStarts(string selector)
    {
        using var fake = new FakeOmpEnvironment();
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync(selector, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.InvalidSelector);
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public void SelectorsMayContainSlashesInTheModelPartAndAreCappedAt256Characters()
    {
        OmpDefaultModel.IsValidSelector("openrouter/anthropic/claude-sonnet-4").ShouldBeTrue();
        OmpDefaultModel.IsValidSelector("acme/" + new string('m', OmpDefaultModel.MaxSelectorLength - 5)).ShouldBeTrue();
        OmpDefaultModel.IsValidSelector("acme/" + new string('m', OmpDefaultModel.MaxSelectorLength - 4)).ShouldBeFalse();
        OmpDefaultModel.IsValidSelector(null).ShouldBeFalse();
    }

    [Fact]
    public async Task AFailedReadStopsTheWriteInsteadOfErasingTheOtherRoles()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(Listing);
        fake.SetRoles("not json");
        var model = new OmpDefaultModel(fake.Options());

        var result = await model.SetDefaultAsync("acme/small", TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpDefaultModelStatus.Failed);
        fake.ReadRoles().ShouldBe("not json");
    }
}
