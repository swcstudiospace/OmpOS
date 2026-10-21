using Bunit;
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
    public void EnterWhileStreamingKeepsTheDraft()
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.IsStreaming, true)
            .Add(p => p.OnSend, (string text) => sent.Add(text)));
        cut.Find("textarea").Input("keep me");

        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        sent.ShouldBeEmpty();

        cut.SetParametersAndRender(parameters => parameters.Add(p => p.IsStreaming, false));
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        sent.Single().ShouldBe("keep me");
    }

    /// <summary>A sent draft is cleared, so the next Enter with no new text sends nothing and the next message starts empty.</summary>
    [Fact]
    public void SendClearsOnlyTheMessageThatWasSent()
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => sent.Add(text)));
        cut.Find("textarea").Input("first");

        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        sent.Single().ShouldBe("first");

        cut.Find("textarea").Input("second");
        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        sent.Count.ShouldBe(2);
        sent[1].ShouldBe("second");
    }
}
