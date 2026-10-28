using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Contracts.Intents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Parser.Tests;

/// <summary>
/// Verifies omp magic-keyword punctuation boundaries and prose masking (PARSE-02).
/// </summary>
public sealed class MagicKeywordBoundaryTests
{
    private static PromptParser CreateParser()
    {
        var services = new ServiceCollection();
        services.AddOmpParser();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<PromptParser>();
    }

    [Fact]
    public void UltrathinkPeriodMatches()
    {
        var envelope = CreateParser().Parse("please ultrathink.");

        envelope.Kind.ShouldBe(IntentKind.Magic);
        envelope.Arguments["keyword"].ShouldBe("ultrathink");
    }

    [Fact]
    public void UltrathinkCommaMatches()
    {
        var envelope = CreateParser().Parse("please ultrathink, then continue");

        envelope.Kind.ShouldBe(IntentKind.Magic);
        envelope.Arguments["keyword"].ShouldBe("ultrathink");
    }

    [Fact]
    public void UltrathinkInParenthesesMatches()
    {
        var envelope = CreateParser().Parse("(ultrathink)");

        envelope.Kind.ShouldBe(IntentKind.Magic);
        envelope.Arguments["keyword"].ShouldBe("ultrathink");
    }

    [Fact]
    public void UltrathinkDotExtensionDoesNotMatch()
    {
        var envelope = CreateParser().Parse("open ultrathink.md");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void UltrathinkSuffixDoesNotMatch()
    {
        var envelope = CreateParser().Parse("ultrathinking");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void UppercaseDoesNotMatch()
    {
        var envelope = CreateParser().Parse("ULTRATHINK");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void DoubleColonPrefixDoesNotMatch()
    {
        var envelope = CreateParser().Parse("foo::ultrathink");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void CallParensDoNotMatch()
    {
        var envelope = CreateParser().Parse("ultrathink()");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void WorkflowzMatches()
    {
        var envelope = CreateParser().Parse("please workflowz this");

        envelope.Kind.ShouldBe(IntentKind.Magic);
        envelope.Arguments["keyword"].ShouldBe("workflowz");
    }

    [Fact]
    public void TripleBacktickFenceDoesNotMatch()
    {
        var envelope = CreateParser().Parse("```\nultrathink\n```");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void TildeFenceDoesNotMatch()
    {
        var envelope = CreateParser().Parse("~~~\nultrathink\n~~~");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Theory]
    [InlineData("run `ultrathink` now")]
    [InlineData("run ``ultrathink`` now")]
    public void InlineCodeSpanMasksAnyRunLength(string raw)
    {
        var envelope = CreateParser().Parse(raw);

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void BalancedSpanContentDoesNotMatch()
    {
        var envelope = CreateParser().Parse("<span>ultrathink</span>");

        envelope.Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void UnbalancedLessThanStillMatches()
    {
        var envelope = CreateParser().Parse("a < b ultrathink");

        envelope.Kind.ShouldBe(IntentKind.Magic);
        envelope.Arguments["keyword"].ShouldBe("ultrathink");
    }
}
