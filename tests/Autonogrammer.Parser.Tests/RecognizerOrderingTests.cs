using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Contracts.Intents;
using Autonogrammer.Parser.Recognizers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Parser.Tests;

/// <summary>
/// Verifies recognizer ordering, DI extensibility, fallback hardwiring and exception containment (PARSE-03..04).
/// </summary>
public sealed class RecognizerOrderingTests
{
    [Fact]
    public void CustomRecognizerWithLowerOrderWinsOverBuiltIns()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPromptRecognizer>(new StubRecognizer(5, static input =>
            input.NormalizedText.StartsWith('/') ? Custom("slash-lookalike") : null));
        services.AddOmpParser();
        using var provider = services.BuildServiceProvider();

        var envelope = provider.GetRequiredService<PromptParser>().Parse("/resume now");

        envelope.Kind.ShouldBe(IntentKind.Chat);
        envelope.Arguments["custom"].ShouldBe("slash-lookalike");
    }

    [Fact]
    public void CustomRecognizerWithHigherOrderRunsAfterBuiltIns()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPromptRecognizer>(new StubRecognizer(500, static _ => Custom("late")));
        services.AddOmpParser();
        using var provider = services.BuildServiceProvider();

        var parser = provider.GetRequiredService<PromptParser>();

        parser.Parse("/resume now").Kind.ShouldBe(IntentKind.SlashCommand);
        parser.Parse("plain words").Arguments["custom"].ShouldBe("late");
    }

    [Fact]
    public void FallbackRegistrationCanBeRemovedAndParserStillTerminates()
    {
        var services = new ServiceCollection();
        services.AddOmpParser();
        foreach (var descriptor in services
                     .Where(static d => d.ServiceType == typeof(IPromptRecognizer) && d.ImplementationType == typeof(FallbackRecognizer))
                     .ToArray())
        {
            services.Remove(descriptor);
        }

        using var provider = services.BuildServiceProvider();
        var parser = provider.GetRequiredService<PromptParser>();

        parser.Parse("hello there").Kind.ShouldBe(IntentKind.Fallback);
        parser.Parse("/resume").Kind.ShouldBe(IntentKind.SlashCommand);
    }

    [Fact]
    public void FallbackRunsLastEvenWhenRegisteredWithOrderZero()
    {
        var parser = new PromptParser(
        [
            new FallbackRecognizer(0),
            new StubRecognizer(100, static input => input.NormalizedText.StartsWith("custom", StringComparison.Ordinal) ? Custom("ran") : null),
        ]);

        parser.Parse("custom text").Arguments["custom"].ShouldBe("ran");
        parser.Parse("other text").Kind.ShouldBe(IntentKind.Fallback);
    }

    [Fact]
    public void RecognizerExceptionBecomesDiagnosticAndPipelineContinues()
    {
        var parser = new PromptParser(
        [
            new StubRecognizer(5, static _ => throw new InvalidOperationException("boom")),
            new SlashCommandRecognizer(),
        ]);

        var envelope = parser.Parse("/resume");

        envelope.Kind.ShouldBe(IntentKind.SlashCommand);
        var diagnostic = envelope.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("recognizer_exception");
        diagnostic.Message.ShouldContain("StubRecognizer");
        diagnostic.Message.ShouldContain("boom");
    }

    [Fact]
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Seeded, deterministic test corpus; not security-sensitive.")]
    public void EveryInputYieldsExactlyOneEnvelope()
    {
        var services = new ServiceCollection();
        services.AddOmpParser();
        using var provider = services.BuildServiceProvider();
        var parser = provider.GetRequiredService<PromptParser>();

        var corpus = new List<string>
        {
            string.Empty,
            " ",
            "\0",
            "/",
            "@",
            "```",
            "`",
            "<!--",
            "ultrathink",
            "\uD800",
        };
        var random = new Random(20261020);
        for (var i = 0; i < 500; i++)
        {
            corpus.Add(RandomPrompt(random));
        }

        foreach (var raw in corpus)
        {
            var envelope = parser.Parse(raw);

            envelope.ShouldNotBeNull();
            envelope.RawText.ShouldBe(raw);
            (envelope.Confidence >= 0.0 && envelope.Confidence <= 1.0).ShouldBeTrue();
        }
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Seeded, deterministic test corpus; not security-sensitive.")]
    private static string RandomPrompt(Random random)
    {
        string[] tokens =
        [
            "/resume", "/", "@src.cs", "@", "Goal:", "key: value", "```", "`", "`docs/a.md`", "<!--", "-->",
            "<b>", "</b>", "also ", "Then ", "ultrathink", "orchestrate", "in src/x.cs", " ", "\n", "\r\n", "\r",
            "\t", "\0", "\u001b", "\u007f", "é", "😀", "\uD800", "\"", "'", "--", ";", "|", "*", "[", "]", "{", "}",
        ];

        var count = random.Next(0, 40);
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = tokens[random.Next(tokens.Length)];
        }

        return string.Concat(parts);
    }

    private static RecognizerResult Custom(string name) => new()
    {
        Kind = IntentKind.Chat,
        Confidence = 1.0,
        Arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["custom"] = name },
        Resources = [],
        RecognizerName = "StubRecognizer",
    };

    /// <summary>A test recognizer whose behaviour is supplied by a delegate.</summary>
    private sealed class StubRecognizer(int order, Func<PromptInput, RecognizerResult?> match) : IPromptRecognizer
    {
        public int Order { get; } = order;

        public RecognizerResult? TryRecognize(PromptInput input) => match(input);
    }
}
