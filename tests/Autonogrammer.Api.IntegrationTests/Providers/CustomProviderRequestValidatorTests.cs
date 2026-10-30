using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Contracts.Providers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>The checks of a custom endpoint request apart from the base URL: id, protocol, display name, models and the key variable.</summary>
public sealed class CustomProviderRequestValidatorTests
{
    private static CustomProviderRequest Valid() => new()
    {
        Id = "my-llm.v2",
        Protocol = CustomProviderProtocol.OpenAi,
        BaseAddress = "https://llm.example.com/v1/",
        ModelIds = ["gpt-4o", "org/model:7b", "a@b+c"],
    };

    [Fact]
    public void AValidRequestBecomesARecordWithTheKeyVariableOfItsId()
    {
        var check = CustomProviderRequestValidator.Validate(Valid(), "alice");

        check.Problem.ShouldBeNull();
        var record = check.Record.ShouldNotBeNull();
        record.Owner.ShouldBe("alice");
        record.DisplayName.ShouldBe("my-llm.v2");
        record.BaseUrl.ShouldBe("https://llm.example.com/v1");
        record.KeyEnvName.ShouldBe("AUTONOGRAMMER_PROVIDER_MY_LLM_V2_KEY");
        record.ModelIds.ShouldBe(["gpt-4o", "org/model:7b", "a@b+c"]);
    }

    [Fact]
    public void AKeylessRequestHasNoKeyVariableAndAGivenNameIsKept()
    {
        var check = CustomProviderRequestValidator.Validate(Valid() with { KeyRequired = false, DisplayName = "  My LLM  " }, "alice");

        var record = check.Record.ShouldNotBeNull();
        record.KeyEnvName.ShouldBeNull();
        record.DisplayName.ShouldBe("My LLM");
    }

    [Theory]
    [InlineData("UPPER")]
    [InlineData("has space")]
    [InlineData("-leading")]
    [InlineData("")]
    [InlineData(null)]
    public void AnIdOmpWouldRefuseIsRefused(string? id)
    {
        CustomProviderRequestValidator.Validate(Valid() with { Id = id }, "alice").Record.ShouldBeNull();
    }

    [Fact]
    public void AMissingProtocolOverlongNameOrBadModelsAreRefused()
    {
        CustomProviderRequestValidator.Validate(Valid() with { Protocol = null }, "alice").Record.ShouldBeNull();
        CustomProviderRequestValidator.Validate(Valid() with { DisplayName = new string('n', 81) }, "alice").Record.ShouldBeNull();
        CustomProviderRequestValidator.Validate(Valid() with { DisplayName = "bad\nname" }, "alice").Record.ShouldBeNull();
        CustomProviderRequestValidator.Validate(Valid() with { ModelIds = null }, "alice").Record.ShouldBeNull();
        CustomProviderRequestValidator.Validate(Valid() with { ModelIds = [new string('m', 129)] }, "alice").Record.ShouldBeNull();
        CustomProviderRequestValidator.Validate(Valid() with { ModelIds = ["-bad"] }, "alice").Record.ShouldBeNull();
    }

    [Fact]
    public void AProblemNamesTheMemberAndNeverEchoesAValue()
    {
        var check = CustomProviderRequestValidator.Validate(Valid() with { BaseAddress = "https://user:topsecret@llm.example.com/v1" }, "alice");

        check.Problem.ShouldNotBeNull().ShouldNotContain("topsecret", Case.Insensitive);
    }

    [Theory]
    [InlineData("deepseek", true)]
    [InlineData("llama.cpp", true)]
    [InlineData("moonshot-cn", true)]
    [InlineData("deepseek-anthropic", true)]
    [InlineData("my-own-llm", false)]
    public void TheCatalogueAndOmpIdsAreReserved(string id, bool reserved)
    {
        CustomProviderRequestValidator.IsReserved(id).ShouldBe(reserved);
    }
}
