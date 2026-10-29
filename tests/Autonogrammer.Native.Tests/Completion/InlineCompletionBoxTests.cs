using System.Net;
using System.Text.Json;
using Autonogrammer.Native.Views.Completion;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Completion;

/// <summary>
/// The inline completion control sends a request only when a provider is configured, shows the suggestion after the caret,
/// and lets Tab accept it at the caret or Escape dismiss it. Every control is named and reachable by Tab.
/// </summary>
public sealed class InlineCompletionBoxTests
{
    private const string Sentence = "hello world";
    private const string Insertion = ", big";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>With no provider configured, typing sends no request and shows no suggestion.</summary>
    [Fact]
    public async Task WithNoProviderConfiguredNoRequestIsSent()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider("{\"suggestion\":\"!\"}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Unconfigured(provider));
            using var window = CompletionHarness.Show(box);

            box.Input.Text = "hello";
            box.Input.CaretIndex = 5;
            await CompletionHarness.PumpAsync(window, TimeSpan.FromMilliseconds(300)).ConfigureAwait(true);

            provider.Requests.ShouldBe(0);
            box.Suggestion.ShouldBeEmpty();
            box.Input.Text.ShouldBe("hello");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With a provider configured, one edit sends exactly one request, carrying the text on each side of the caret.</summary>
    [Fact]
    public async Task WithAProviderConfiguredOneEditSendsExactlyOneRequest()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider("{\"suggestion\":\"!\"}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Configured(provider));
            using var window = CompletionHarness.Show(box);

            box.Input.Text = "hello";
            box.Input.CaretIndex = 5;
            await CompletionHarness.WaitForAsync(window, () => SuggestionIs(box, "!"), "the suggestion").ConfigureAwait(true);
            await CompletionHarness.PumpAsync(window, TimeSpan.FromMilliseconds(200)).ConfigureAwait(true);

            provider.Requests.ShouldBe(1);
            using var request = JsonDocument.Parse(provider.RequestBodies[0]);
            request.RootElement.GetProperty("prefix").GetString().ShouldBe("hello");
            request.RootElement.GetProperty("suffix").GetString().ShouldBeEmpty();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Tab inserts the suggestion at the caret, moves the caret past it, and sends no further request.</summary>
    [Fact]
    public async Task TabInsertsTheSuggestionAtTheCaret()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider($"{{\"suggestion\":\"{Insertion}\"}}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Configured(provider));
            using var window = CompletionHarness.Show(box);
            box.Input.Focus();

            box.Input.Text = Sentence;
            box.Input.CaretIndex = 5;
            await CompletionHarness.WaitForAsync(window, () => SuggestionIs(box, Insertion), "the suggestion").ConfigureAwait(true);
            AutomationProperties.GetHelpText(box.Input).ShouldNotBeNullOrWhiteSpace("a shown suggestion names its keys");

            CompletionHarness.Press(window, Key.Tab, PhysicalKey.Tab);

            box.Input.Text.ShouldBe("hello, big world");
            box.Input.CaretIndex.ShouldBe(5 + Insertion.Length);
            box.Suggestion.ShouldBeEmpty();
            AutomationProperties.GetHelpText(box.Input).ShouldBeNullOrEmpty();

            await CompletionHarness.PumpAsync(window, TimeSpan.FromMilliseconds(200)).ConfigureAwait(true);
            provider.Requests.ShouldBe(1, "accepting a suggestion asks for nothing more");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Escape dismisses the suggestion: the text and caret are unchanged, and no further request is sent.</summary>
    [Fact]
    public async Task EscapeDismissesTheSuggestionAndInsertsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider($"{{\"suggestion\":\"{Insertion}\"}}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Configured(provider));
            using var window = CompletionHarness.Show(box);
            box.Input.Focus();

            box.Input.Text = Sentence;
            box.Input.CaretIndex = 5;
            await CompletionHarness.WaitForAsync(window, () => SuggestionIs(box, Insertion), "the suggestion").ConfigureAwait(true);

            CompletionHarness.Press(window, Key.Escape, PhysicalKey.Escape);

            box.Input.Text.ShouldBe(Sentence);
            box.Input.CaretIndex.ShouldBe(5);
            box.Suggestion.ShouldBeEmpty();
            AutomationProperties.GetHelpText(box.Input).ShouldBeNullOrEmpty();

            await CompletionHarness.PumpAsync(window, TimeSpan.FromMilliseconds(200)).ConfigureAwait(true);
            provider.Requests.ShouldBe(1, "a dismissed suggestion is not asked for again until the text changes");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>An edit discards the shown suggestion, and the new text is sent for a fresh one.</summary>
    [Fact]
    public async Task AnEditDiscardsTheShownSuggestionAndAsksAgain()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider($"{{\"suggestion\":\"{Insertion}\"}}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Configured(provider));
            using var window = CompletionHarness.Show(box);

            box.Input.Text = Sentence;
            box.Input.CaretIndex = 5;
            await CompletionHarness.WaitForAsync(window, () => SuggestionIs(box, Insertion), "the first suggestion").ConfigureAwait(true);

            box.Input.Text = "hello there world";
            box.Input.CaretIndex = 5;
            box.Suggestion.ShouldBeEmpty("the stale suggestion is removed as soon as the text changes");

            await CompletionHarness.WaitForAsync(window, () => SuggestionIs(box, Insertion), "the second suggestion").ConfigureAwait(true);
            provider.Requests.ShouldBe(2);
            using var second = JsonDocument.Parse(provider.RequestBodies[1]);
            second.RootElement.GetProperty("prefix").GetString().ShouldBe("hello");
            second.RootElement.GetProperty("suffix").GetString().ShouldBe(" there world");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With no suggestion shown, Tab keeps its usual meaning and moves focus out of the box.</summary>
    [Fact]
    public async Task TabWithNoSuggestionMovesFocusOutOfTheBox()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider("{\"suggestion\":\"!\"}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Unconfigured(provider));
            var after = new Button { Content = "After" };
            using var window = CompletionHarness.Show(new StackPanel { Children = { box, after } });

            box.Input.Text = Sentence;
            box.Input.CaretIndex = 5;
            box.Input.Focus();
            CompletionHarness.Press(window, Key.Tab, PhysicalKey.Tab);

            after.IsFocused.ShouldBeTrue();
            box.Input.Text.ShouldBe(Sentence);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A provider that answers with an error status is reported, inserts nothing, and leaves focus movement working.</summary>
    [Fact]
    public async Task AFailedProviderIsReportedAndInsertsNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider("{}", HttpStatusCode.InternalServerError);

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Configured(provider));
            var after = new Button { Content = "After" };
            using var window = CompletionHarness.Show(new StackPanel { Children = { box, after } });
            var status = box.FindControl<TextBlock>("UnavailableText")!;

            box.Input.Text = Sentence;
            box.Input.CaretIndex = 5;
            await CompletionHarness.WaitForAsync(window, () => status.IsVisible, "the unavailable status").ConfigureAwait(true);

            box.Suggestion.ShouldBeEmpty();
            box.Input.Text.ShouldBe(Sentence);

            box.Input.Focus();
            CompletionHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            after.IsFocused.ShouldBeTrue("with nothing to accept, Tab still moves focus");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every control of the completion box has an accessible name, and the box is reachable with Tab.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndReachableByTab()
    {
        await using var session = TestAppBuilder.StartSession();
        using var provider = new CountingProvider("{\"suggestion\":\"!\"}");

        await session.RunAsync(async () =>
        {
            var box = CompletionHarness.NewBox(CompletionHarness.Configured(provider));
            var before = new Button { Content = "Before" };
            using var window = CompletionHarness.Show(new StackPanel { Children = { before, box } });

            AutomationProperties.GetName(box.Input).ShouldNotBeNullOrWhiteSpace();
            AutomationProperties.GetName(box.FindControl<TextBlock>("GhostText")!).ShouldNotBeNullOrWhiteSpace();
            AutomationProperties.GetName(box.FindControl<TextBlock>("UnavailableText")!).ShouldNotBeNullOrWhiteSpace();

            before.Focus();
            CompletionHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            box.Input.IsFocused.ShouldBeTrue("Tab from the control before reaches the text box");
        }, Token).ConfigureAwait(true);
    }

    // Ordinal comparison: the suggestion is compared as the exact text the provider sent.
    private static bool SuggestionIs(InlineCompletionBox box, string text) =>
        string.Equals(box.Suggestion, text, StringComparison.Ordinal);
}
