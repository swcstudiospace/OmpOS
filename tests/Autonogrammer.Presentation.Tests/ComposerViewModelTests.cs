using Autonogrammer.Presentation.Composer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// Composer send gating, clear-only-on-accept, and key mapping, tested on <see cref="ComposerViewModel"/> with
/// primitive key input and no UI framework (UI-03, IN-02, XP-08). The bUnit originals stay in Autonogrammer.UI.Tests.
/// </summary>
public sealed class ComposerViewModelTests
{
    /// <summary>Enter while a response streams keeps the typed draft for the next send.</summary>
    [Fact]
    public async Task EnterWhileStreamingKeepsTheDraft()
    {
        var sent = new List<string>();
        var model = new ComposerViewModel(text => AcceptAsync(sent, text), NoStopAsync)
        {
            Draft = "keep me",
            IsStreaming = true,
        };

        await model.HandleKeyAsync("Enter", shift: false);
        sent.ShouldBeEmpty();

        model.IsStreaming = false;
        await model.HandleKeyAsync("Enter", shift: false);

        sent.Single().ShouldBe("keep me");
    }

    /// <summary>A send the host refuses keeps the draft, so the next Enter submits the same text again.</summary>
    [Fact]
    public async Task RefusedSendKeepsTheDraft()
    {
        var sent = new List<string>();
        var model = new ComposerViewModel(text => RefuseAsync(sent, text), NoStopAsync)
        {
            Draft = "keep me",
        };

        await model.HandleKeyAsync("Enter", shift: false);
        await model.HandleKeyAsync("Enter", shift: false);

        sent.ShouldBe(["keep me", "keep me"]);
    }

    /// <summary>An accepted send clears the draft: the next Enter with no new text sends nothing, and the next message starts empty.</summary>
    [Fact]
    public async Task AcceptedSendClearsTheDraft()
    {
        var sent = new List<string>();
        var model = new ComposerViewModel(text => AcceptAsync(sent, text), NoStopAsync)
        {
            Draft = "first",
        };

        await model.HandleKeyAsync("Enter", shift: false);
        sent.Single().ShouldBe("first");
        model.Draft.ShouldBeEmpty();

        await model.HandleKeyAsync("Enter", shift: false);
        sent.Count.ShouldBe(1);

        model.Draft = "second";
        await model.HandleKeyAsync("Enter", shift: false);

        sent.ShouldBe(["first", "second"]);
    }

    /// <summary>A second Enter while a send is still pending does not start a concurrent send of the same draft.</summary>
    [Fact]
    public async Task EnterWhileASendIsPendingDoesNotResend()
    {
        var sent = new List<string>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new ComposerViewModel(
            text =>
            {
                sent.Add(text);
#pragma warning disable VSTHRD003 // Test helper: the send stays open until the test thread releases it, by design.
                return release.Task;
#pragma warning restore VSTHRD003
            },
            NoStopAsync)
        {
            Draft = "once",
        };

        var pending = model.HandleKeyAsync("Enter", shift: false);
        await model.HandleKeyAsync("Enter", shift: false);
        release.SetResult(true);
        await pending.ConfigureAwait(true);

        sent.ShouldBe(["once"]);
    }

    /// <summary>A draft made only of whitespace is never sent by Enter (IN-02, TD-38).</summary>
    /// <param name="draft">The whitespace-only draft text.</param>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \r\n\t ")]
    public async Task WhitespaceOnlyDraftIsNotSentByEnter(string draft)
    {
        var sent = new List<string>();
        var model = new ComposerViewModel(text => AcceptAsync(sent, text), NoStopAsync)
        {
            Draft = draft,
        };

        await model.HandleKeyAsync("Enter", shift: false);

        sent.ShouldBeEmpty();
    }

    /// <summary>A whitespace-only draft is never sent by the send action either (IN-02, TD-38).</summary>
    /// <param name="draft">The whitespace-only draft text.</param>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \r\n\t ")]
    public async Task WhitespaceOnlyDraftIsNotSentBySendAction(string draft)
    {
        var sent = new List<string>();
        var model = new ComposerViewModel(text => AcceptAsync(sent, text), NoStopAsync)
        {
            Draft = draft,
        };

        await model.SendAsync();

        sent.ShouldBeEmpty();
    }

    /// <summary>Enter without Shift sends; Shift+Enter inserts a newline and sends nothing.</summary>
    [Fact]
    public async Task EnterWithoutShiftSendsWhileShiftEnterDoesNot()
    {
        var sent = new List<string>();
        var model = new ComposerViewModel(text => AcceptAsync(sent, text), NoStopAsync)
        {
            Draft = "hello",
        };

        await model.HandleKeyAsync("Enter", shift: true);
        sent.ShouldBeEmpty();

        await model.HandleKeyAsync("Enter", shift: false);
        sent.Single().ShouldBe("hello");
    }

    /// <summary>Escape stops the response only while one is streaming (UI-03).</summary>
    [Fact]
    public async Task EscapeStopsOnlyWhileStreaming()
    {
        var stops = 0;
        var model = new ComposerViewModel(text => AcceptAsync(new List<string>(), text), () => CountStopAsync(() => stops++))
        {
            IsStreaming = false,
        };

        await model.HandleKeyAsync("Escape", shift: false);
        stops.ShouldBe(0);

        model.IsStreaming = true;
        await model.HandleKeyAsync("Escape", shift: false);
        stops.ShouldBe(1);
    }

    private static Task<bool> AcceptAsync(List<string> sent, string text)
    {
        sent.Add(text);
        return Task.FromResult(true);
    }

    private static Task<bool> RefuseAsync(List<string> sent, string text)
    {
        sent.Add(text);
        return Task.FromResult(false);
    }

    private static Task NoStopAsync() => Task.CompletedTask;

    private static Task CountStopAsync(Action count)
    {
        count();
        return Task.CompletedTask;
    }
}
