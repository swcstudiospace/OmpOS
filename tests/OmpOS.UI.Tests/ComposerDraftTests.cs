using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.UI.Components;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>Enter sends only what the parent accepts; a refused send keeps the typed draft (UI-03).</summary>
public sealed class ComposerDraftTests : UiTestContext
{
    /// <summary>Enter while a response streams keeps the typed draft for the next send.</summary>
    [Fact]
    public async Task EnterWhileStreamingKeepsTheDraft()
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.IsStreaming, true)
            .Add(p => p.OnSend, (string text) => AcceptAsync(sent, text)));
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "keep me" });

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        sent.ShouldBeEmpty();

        cut.Render(parameters => parameters.Add(p => p.IsStreaming, false));
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        sent.Single().ShouldBe("keep me");
    }

    /// <summary>A send the parent refuses keeps the draft, so the next Enter submits the same text again.</summary>
    [Fact]
    public async Task RefusedSendKeepsTheDraft()
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => RefuseAsync(sent, text)));
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "keep me" });

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        sent.ShouldBe(["keep me", "keep me"]);
    }

    /// <summary>An accepted send clears the draft: the next Enter with no new text sends nothing, and the next message starts empty.</summary>
    [Fact]
    public async Task AcceptedSendClearsTheDraft()
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => AcceptAsync(sent, text)));
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "first" });

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        sent.Single().ShouldBe("first");

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "second" });
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        sent.ShouldBe(["first", "second"]);
    }

    /// <summary>A second Enter while a send is still pending does not start a concurrent send of the same draft.</summary>
    [Fact]
    public async Task EnterWhileASendIsPendingDoesNotResend()
    {
        var sent = new List<string>();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) =>
            {
                sent.Add(text);
#pragma warning disable VSTHRD003 // Test helper: the send stays open until the test thread releases it, by design.
                return release.Task;
#pragma warning restore VSTHRD003
            }));
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "once" });

        var pending = cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        release.SetResult(true);
        await pending.ConfigureAwait(true);

        sent.ShouldBe(["once"]);
    }
}
