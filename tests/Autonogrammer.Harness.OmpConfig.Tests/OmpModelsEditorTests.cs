using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpModelsEditorTests
{
    public static TheoryData<string, string> Texts() => OmpModelsFixtures.Texts();

    [Theory]
    [MemberData(nameof(Texts))]
    public void UpsertThenRemoveRestoresTheOriginalBytesExactly(string name, string original)
    {
        var added = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local());

        added.Status.ShouldBe(OmpModelsWriteStatus.Written, name);
        added.NewText.ShouldNotBeNull();

        var removed = OmpModelsEditor.Remove(added.NewText, "acme");

        removed.Status.ShouldBe(OmpModelsWriteStatus.Written, name);
        removed.NewText.ShouldBe(original);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void TwoProvidersAddedAndRemovedInEitherOrderRestoreTheOriginalBytes(string name, string original)
    {
        var first = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local("first")).NewText!;
        var both = OmpModelsEditor.Upsert(first, OmpModelsFixtures.Keyed("second")).NewText!;

        var withoutFirst = OmpModelsEditor.Remove(both, "first");
        withoutFirst.Status.ShouldBe(OmpModelsWriteStatus.Written, name);
        OmpModelsEditor.Remove(withoutFirst.NewText!, "second").NewText.ShouldBe(original);

        var withoutSecond = OmpModelsEditor.Remove(both, "second");
        withoutSecond.Status.ShouldBe(OmpModelsWriteStatus.Written, name);
        OmpModelsEditor.Remove(withoutSecond.NewText!, "first").NewText.ShouldBe(original);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void UpsertingTwiceIsIdempotent(string name, string original)
    {
        var first = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Keyed());
        var second = OmpModelsEditor.Upsert(first.NewText!, OmpModelsFixtures.Keyed());

        second.Status.ShouldBe(OmpModelsWriteStatus.Unchanged, name);
        second.NewText.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void ReplacingABlockChangesNothingOutsideIt(string name, string original)
    {
        var first = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local()).NewText!;
        var changed = OmpModelsFixtures.Local() with { BaseUrl = new Uri("http://127.0.0.1:8080/v1"), Models = [new OmpManagedModel("big"), new OmpManagedModel("small")] };

        var second = OmpModelsEditor.Upsert(first, changed);

        second.Status.ShouldBe(OmpModelsWriteStatus.Written, name);
        second.NewText.ShouldNotBeNull().ShouldContain("8080", Case.Sensitive);
        OmpModelsEditor.Remove(second.NewText!, "acme").NewText.ShouldBe(original);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void WhatTheEditorWritesReadsBackEqual(string name, string original)
    {
        var text = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Keyed()).NewText!;
        text = OmpModelsEditor.Upsert(text, OmpModelsFixtures.Local()).NewText!;

        var read = OmpModelsEditor.ReadManaged(text);

        read.Problems.ShouldBeEmpty(name);
        read.Providers.Select(provider => provider.Id).ShouldBe(["gateway", "acme"]);
        AssertEqual(OmpModelsFixtures.Keyed(), read.Providers[0]);
        AssertEqual(OmpModelsFixtures.Local(), read.Providers[1]);
    }

    [Fact]
    public void ACreatedFileStartsWithProvidersAndTheBlock()
    {
        var added = OmpModelsEditor.Upsert(string.Empty, OmpModelsFixtures.Local());

        added.NewText.ShouldBe(
            "providers: # autonogrammer:created\n" +
            "  # autonogrammer:begin acme\n" +
            "  \"acme\":\n" +
            "    baseUrl: \"http://127.0.0.1:11434/v1\"\n" +
            "    api: openai-completions\n" +
            "    auth: none\n" +
            "    models:\n" +
            "      - id: \"small\"\n" +
            "        reasoning: false\n" +
            "        input: [text]\n" +
            "  # autonogrammer:end acme\n");
    }

    [Fact]
    public void AKeyedAnthropicProviderNamesTheEnvironmentVariableAndAsksForPlainKeyShaping()
    {
        var text = OmpModelsEditor.Upsert(string.Empty, OmpModelsFixtures.Keyed()).NewText!;

        text.ShouldContain("    apiKey: \"GATEWAY_API_KEY\"\n", Case.Sensitive);
        text.ShouldContain("    auth: apiKey\n", Case.Sensitive);
        text.ShouldContain("    api: anthropic-messages\n", Case.Sensitive);
        text.ShouldContain("      - id: \"llama3:8b\"\n", Case.Sensitive);
        text.ShouldContain("      - id: \"123\"\n", Case.Sensitive);
        text.ShouldContain("        name: \"Claude \\\"Sonnet\\\" \\\\ ünï\"\n", Case.Sensitive);
        text.ShouldContain("        contextWindow: 200000\n", Case.Sensitive);
        text.ShouldContain("        input: [text, image]\n", Case.Sensitive);
    }

    [Fact]
    public void AKeyedOpenAiProviderOmitsTheAuthModeLine()
    {
        var provider = OmpModelsFixtures.Keyed() with { Api = OmpProviderApi.OpenAiResponses };

        var text = OmpModelsEditor.Upsert(string.Empty, provider).NewText!;

        text.ShouldContain("    apiKey: \"GATEWAY_API_KEY\"\n", Case.Sensitive);
        text.ShouldContain("    api: openai-responses\n", Case.Sensitive);
        text.ShouldNotContain("auth:", Case.Sensitive);
    }

    [Fact]
    public void ABlockInAFourSpaceFileUsesFourSpaceLevels()
    {
        var original = "providers:\n    a:\n        baseUrl: x\n";

        var text = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local()).NewText!;

        text.ShouldBe(
            original +
            "    # autonogrammer:begin acme\n" +
            "    \"acme\":\n" +
            "        baseUrl: \"http://127.0.0.1:11434/v1\"\n" +
            "        api: openai-completions\n" +
            "        auth: none\n" +
            "        models:\n" +
            "            - id: \"small\"\n" +
            "              reasoning: false\n" +
            "              input: [text]\n" +
            "    # autonogrammer:end acme\n");
    }

    [Fact]
    public void ACrlfFileGetsCrlfLinesInTheBlock()
    {
        var text = OmpModelsEditor.Upsert("providers:\r\n  a:\r\n    x: 1\r\n", OmpModelsFixtures.Local()).NewText!;

        text.Replace("\r\n", string.Empty, StringComparison.Ordinal).ShouldNotContain("\n", Case.Sensitive);
        text.ShouldContain("  # autonogrammer:begin acme\r\n", Case.Sensitive);
    }

    [Fact]
    public void ANewBlockGoesAfterTheLastChildAndBeforeTheNextRootKey()
    {
        var original = "model: x\nproviders:\n  a:\n    x: 1\n# about the next key\nmodelRoles:\n  default: a/b\n";

        var text = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local()).NewText!;

        var beginAt = text.IndexOf("  # autonogrammer:begin acme", StringComparison.Ordinal);
        beginAt.ShouldBeGreaterThan(text.IndexOf("    x: 1\n", StringComparison.Ordinal));
        beginAt.ShouldBeLessThan(text.IndexOf("# about the next key", StringComparison.Ordinal));
        text.EndsWith("modelRoles:\n  default: a/b\n", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Theory]
    [InlineData("providers:\n  acme:\n    baseUrl: x\n")]
    [InlineData("providers:\n  \"acme\":\n    baseUrl: x\n")]
    [InlineData("providers:\n  'acme':\n    baseUrl: x\n")]
    [InlineData("providers:\n  acme: {baseUrl: x}\n")]
    public void AnIdDefinedOutsideABlockIsAConflictAndIsNeverRemoved(string original)
    {
        var upsert = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local());
        var remove = OmpModelsEditor.Remove(original, "acme");

        upsert.Status.ShouldBe(OmpModelsWriteStatus.Conflict);
        upsert.NewText.ShouldBeNull();
        remove.Status.ShouldBe(OmpModelsWriteStatus.Conflict);
        remove.NewText.ShouldBeNull();
    }

    [Fact]
    public void RemovingAProviderThatIsNotThereChangesNothing()
    {
        var result = OmpModelsEditor.Remove("providers:\n  a:\n    x: 1\n", "acme");

        result.Status.ShouldBe(OmpModelsWriteStatus.Unchanged);
        result.NewText.ShouldBeNull();
    }

    [Theory]
    [InlineData("providers: {}\n")]
    [InlineData("providers: {a: 1}\n")]
    [InlineData("providers: []\n")]
    [InlineData("providers: null\n")]
    [InlineData("providers: ~\n")]
    [InlineData("providers:\n- a\n- b\n")]
    [InlineData("providers:\n  - a\n")]
    [InlineData("providers:\n  {a: 1}\n")]
    [InlineData("providers:\n\ta:\n\t\tx: 1\n")]
    [InlineData("providers:\n  a:\n\tx: 1\n")]
    [InlineData("providers:\n  a: 1\nproviders:\n  b: 2\n")]
    [InlineData("---\nproviders:\n  a: 1\n")]
    [InlineData("providers:\n  a: 1\n...\n")]
    [InlineData("providers:\r  a: 1\r")]
    [InlineData("providers:\n  # autonogrammer:begin acme\n  \"acme\":\n    x: 1\n")]
    [InlineData("providers:\n  # autonogrammer:end acme\n")]
    [InlineData("providers:\n  # autonogrammer:begin acme\n  # autonogrammer:begin other\n  # autonogrammer:end other\n  # autonogrammer:end acme\n")]
    [InlineData("providers:\n  # autonogrammer:begin acme\n  # autonogrammer:end acme\n  # autonogrammer:begin acme\n  # autonogrammer:end acme\n")]
    [InlineData("providers:\n  a:\n    x: 1\n    # autonogrammer:begin acme\n    # autonogrammer:end acme\n")]
    public void ALayoutTheEditorDoesNotUnderstandIsRefusedAndNeverTouched(string original)
    {
        var upsert = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local());
        var remove = OmpModelsEditor.Remove(original, "acme");
        var read = OmpModelsEditor.ReadManaged(original);

        upsert.Status.ShouldBe(OmpModelsWriteStatus.UnsupportedLayout);
        upsert.NewText.ShouldBeNull();
        upsert.Message.ShouldContain("by hand", Case.Insensitive);
        remove.Status.ShouldBe(OmpModelsWriteStatus.UnsupportedLayout);
        read.Providers.ShouldBeEmpty();
        read.Problems.ShouldNotBeEmpty();
    }

    [Fact]
    public void AFlowStyleProvidersMappingIsRefusedWithAnExplanation()
    {
        var result = OmpModelsEditor.Upsert("providers: {}\n", OmpModelsFixtures.Local());

        result.Status.ShouldBe(OmpModelsWriteStatus.UnsupportedLayout);
        result.Message.ShouldContain("flow style", Case.Insensitive);
    }

    [Fact]
    public void ABlockEditedByHandIsReportedAsAProblemAndTheOtherBlocksStillRead()
    {
        var text = OmpModelsEditor.Upsert(string.Empty, OmpModelsFixtures.Local("good")).NewText!;
        text = OmpModelsEditor.Upsert(text, OmpModelsFixtures.Local("hand-edited")).NewText!;
        text = text.Replace(
            "  \"hand-edited\":\n",
            "  \"hand-edited\":\n    headers: {x: y}\n",
            StringComparison.Ordinal);

        var read = OmpModelsEditor.ReadManaged(text);

        read.Providers.Select(provider => provider.Id).ShouldBe(["good"]);
        read.Problems.Count.ShouldBe(1);
        read.Problems[0].ShouldContain("hand-edited", Case.Sensitive);
    }

    [Fact]
    public void AFileWithoutProvidersReadsAsNoManagedProviders()
    {
        var read = OmpModelsEditor.ReadManaged("# nothing here\nmodel: x\n");

        read.Providers.ShouldBeEmpty();
        read.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void ForeignProvidersAreNeverReadAsManaged()
    {
        var read = OmpModelsEditor.ReadManaged("providers:\n  a:\n    baseUrl: x\n    api: openai-completions\n    apiKey: A_KEY\n    models:\n      - id: m\n");

        read.Providers.ShouldBeEmpty();
        read.Problems.ShouldBeEmpty();
    }

    private static void AssertEqual(OmpManagedProvider expected, OmpManagedProvider actual)
    {
        actual.Id.ShouldBe(expected.Id);
        actual.BaseUrl.ShouldBe(expected.BaseUrl);
        actual.Api.ShouldBe(expected.Api);
        actual.ApiKeyEnvName.ShouldBe(expected.ApiKeyEnvName);
        actual.NoAuthentication.ShouldBe(expected.NoAuthentication);
        actual.Models.ShouldBe(expected.Models);
    }
}
