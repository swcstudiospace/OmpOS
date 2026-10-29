using Autonogrammer.UI.Components;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>A draft made only of whitespace is never sent, by Enter or by the send control (IN-02, TD-38).</summary>
public sealed class ComposerWhitespaceDraftTests : UiTestContext
{
    /// <summary>Enter with a whitespace-only draft sends nothing.</summary>
    /// <param name="draft">The whitespace-only draft text.</param>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \r\n\t ")]
    public async Task WhitespaceOnlyDraftIsNotSentByEnter(string draft)
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => AcceptAsync(sent, text)));
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = draft });

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        sent.ShouldBeEmpty();
    }

    /// <summary>The send control with a whitespace-only draft sends nothing.</summary>
    /// <param name="draft">The whitespace-only draft text.</param>
    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \r\n\t ")]
    public async Task WhitespaceOnlyDraftIsNotSentByTheSendButton(string draft)
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => AcceptAsync(sent, text)));
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = draft });

        await cut.Find("button[aria-label='Send message']").ClickAsync(new MouseEventArgs());

        sent.ShouldBeEmpty();
    }
}
