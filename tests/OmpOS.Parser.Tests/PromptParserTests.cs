using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Intents;
using Shouldly;
using Xunit;

namespace OmpOS.Parser.Tests;

/// <summary>
/// Verifies the observable parse rules of the catch-all prompt parser (PARSE-01..03).
/// </summary>
public sealed class PromptParserTests
{
    private static PromptParser CreateParser()
    {
        var services = new ServiceCollection();
        services.AddOmpParser();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<PromptParser>();
    }

    [Fact]
    public void EmptyStringYieldsFallbackEnvelope()
    {
        var envelope = CreateParser().Parse(string.Empty);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
        envelope.RawText.ShouldBe(string.Empty);
        envelope.NormalizedText.ShouldBe(string.Empty);
        envelope.Confidence.ShouldBe(0.0);
        envelope.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void NullInputIsTreatedAsEmpty()
    {
        var envelope = CreateParser().Parse(null);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
        envelope.RawText.ShouldBe(string.Empty);
    }

    [Fact]
    public void WhitespaceOnlyInputPreservesRawAndNormalizesToEmpty()
    {
        const string raw = "  \t \n\r\n  ";

        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
        envelope.RawText.ShouldBe(raw);
        envelope.NormalizedText.ShouldBe(string.Empty);
    }

    [Fact]
    public void OversizedPromptIsTruncatedWithDiagnostic()
    {
        var raw = new string('a', PromptParser.MaxPromptLength + 1);

        var envelope = CreateParser().Parse(raw);

        envelope.RawText.Length.ShouldBe(PromptParser.MaxPromptLength);
        envelope.Kind.ShouldBe(IntentKind.Fallback);
        var diagnostic = envelope.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("prompt_truncated");
        diagnostic.Message.ShouldContain("64001");
    }

    [Fact]
    public void PromptAtLimitIsNotTruncated()
    {
        var raw = new string('a', PromptParser.MaxPromptLength);

        var envelope = CreateParser().Parse(raw);

        envelope.RawText.ShouldBe(raw);
        envelope.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void TruncationDoesNotSplitSurrogatePair()
    {
        var raw = new string('a', PromptParser.MaxPromptLength - 1) + "😀b";

        var envelope = CreateParser().Parse(raw);

        envelope.RawText.Length.ShouldBe(PromptParser.MaxPromptLength - 1);
        char.IsHighSurrogate(envelope.RawText[^1]).ShouldBeFalse();
    }

    [Fact]
    public void MixedNewlinesNormalizeToLineFeedAndRawIsKept()
    {
        const string raw = "a\r\nb\rc\nd";

        var envelope = CreateParser().Parse(raw);

        envelope.RawText.ShouldBe(raw);
        envelope.NormalizedText.ShouldBe("a\nb\nc\nd");
    }

    [Fact]
    public void SlashCommandWithCrLfArgumentsNormalizesNewlines()
    {
        var envelope = CreateParser().Parse("/resume\r\nfirst\rsecond");

        envelope.Kind.ShouldBe(IntentKind.SlashCommand);
        envelope.Arguments["command"].ShouldBe("resume");
        envelope.Arguments["args"].ShouldBe("first\nsecond");
    }

    [Fact]
    public void UnicodeAndEmojiArePreserved()
    {
        const string raw = "héllo 🌍 世界 — ✓";

        var envelope = CreateParser().Parse(raw);

        envelope.RawText.ShouldBe(raw);
        envelope.NormalizedText.ShouldBe(raw);
        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void ControlCharactersAreKeptInRawAndStrippedFromNormalized()
    {
        const string raw = "a\u0000b\u0007c\u001b d\u007f";

        var envelope = CreateParser().Parse(raw);

        envelope.RawText.ShouldBe(raw);
        envelope.NormalizedText.ShouldBe("abc d");
    }

    [Fact]
    public void InjectionStyleTextIsPlainFallback()
    {
        const string raw = "\"; drop table --' OR 1=1; /* */ ${{ 7*7 }}";

        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
        envelope.RawText.ShouldBe(raw);
        envelope.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void SlashCommandOnFirstLineIsRecognized()
    {
        var envelope = CreateParser().Parse("/resume  latest session");

        envelope.Kind.ShouldBe(IntentKind.SlashCommand);
        envelope.Arguments["command"].ShouldBe("resume");
        envelope.Arguments["args"].ShouldBe("latest session");
    }

    [Fact]
    public void SlashCommandInsideFenceIsPlainText()
    {
        var envelope = CreateParser().Parse("```\n/resume\n```");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void SlashCommandAfterFirstLineIsNotRecognized()
    {
        var envelope = CreateParser().Parse("hello\n/resume");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void MentionsAndFileReferencesBecomeResources()
    {
        const string raw = "look at @src.cs and @Foo-bar in src/app/Program.cs and `docs/readme.md`";

        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Mention);
        envelope.Resources.ShouldBe(["@src.cs", "@Foo-bar", "src/app/Program.cs", "docs/readme.md"]);
    }

    [Fact]
    public void EmailAddressIsNotAMention()
    {
        var envelope = CreateParser().Parse("mail me at dev@example.com");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void DirectiveBlockParsesKeyValuePairs()
    {
        var envelope = CreateParser().Parse("Goal: ship the parser\nPriority: high\n");

        envelope.Kind.ShouldBe(IntentKind.Directive);
        envelope.Arguments["Goal"].ShouldBe("ship the parser");
        envelope.Arguments["Priority"].ShouldBe("high");
    }

    [Fact]
    public void UrlIsNotADirective()
    {
        var envelope = CreateParser().Parse("https://example.com/docs");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Theory]
    [InlineData("ultrathink", "ultrathink")]
    [InlineData("please orchestrate this", "orchestrate")]
    [InlineData("workflowz", "workflowz")]
    [InlineData("run jevify now", "jevify")]
    public void MagicKeywordStandaloneIsRecognized(string raw, string keyword)
    {
        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Magic);
        envelope.Arguments["keyword"].ShouldBe(keyword);
    }

    [Theory]
    [InlineData("```\nultrathink\n```")]
    [InlineData("run `orchestrate` now")]
    [InlineData("<span title=\"jevify\">x</span>")]
    [InlineData("<!-- ultrathink -->")]
    public void MagicKeywordInsideFenceInlineCodeOrHtmlIsIgnored(string raw)
    {
        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Theory]
    [InlineData("ULTRATHINK")]
    [InlineData("Ultrathink now")]
    [InlineData("ultrathinking")]
    public void MagicKeywordIsCaseSensitiveAndBoundaryDelimited(string raw)
    {
        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void SteerWhileSessionRunningIsSteer()
    {
        var envelope = CreateParser().Parse("actually use the other file", new PromptContext { IsSessionRunning = true });

        envelope.Kind.ShouldBe(IntentKind.Steer);
        envelope.Confidence.ShouldBeGreaterThan(0.0);
    }

    [Fact]
    public void EmptySteerWhileSessionRunningIsFallback()
    {
        var envelope = CreateParser().Parse(string.Empty, new PromptContext { IsSessionRunning = true });

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Theory]
    [InlineData("also add tests")]
    [InlineData("Then rerun it")]
    public void FollowUpMarkerAfterCompletedPromptIsFollowUp(string raw)
    {
        var envelope = CreateParser().Parse(raw, new PromptContext { HasCompletedPrompt = true });

        envelope.Kind.ShouldBe(IntentKind.FollowUp);
    }

    [Fact]
    public void FollowUpMarkerWithoutCompletedPromptIsFallback()
    {
        var envelope = CreateParser().Parse("also add tests");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }
}
