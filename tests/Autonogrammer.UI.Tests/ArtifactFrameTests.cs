using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.UI.Components.Cowork.Artifacts;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// The web artifact frame (D-21 to D-23): the exact sandbox of an interactive document, the static view labelled as such, the closing of a
/// frame on a second load, the closing of every frame on sign-out, and the rule that no artifact component posts a message.
/// </summary>
public sealed class ArtifactFrameTests : UiTestContext
{
    private const string TicketUrl = "https://api.example.invalid/cowork/artifact-render/ticket";

    /// <summary>An interactive document is framed with exactly the sandbox, referrer and permission attributes of D-23.</summary>
    [Fact]
    public void AnInteractiveFrameHasTheExactSandboxAndPolicyAttributes()
    {
        var cut = Render<ArtifactFrame>(parameters => parameters
            .Add(p => p.Title, "Interactive artifact")
            .Add(p => p.RenderUrl, TicketUrl));

        var frame = cut.Find("iframe");
        frame.GetAttribute("sandbox").ShouldBe("allow-scripts");
        frame.GetAttribute("referrerpolicy").ShouldBe("no-referrer");
        frame.GetAttribute("allow").ShouldBe(string.Empty);
        frame.GetAttribute("src").ShouldBe(TicketUrl);
        frame.HasAttribute("srcdoc").ShouldBeFalse();
        frame.GetAttribute("title").ShouldBe("Interactive artifact");
    }

    /// <summary>The first load keeps the frame; a second load destroys it and reports the blocked navigation.</summary>
    [Fact]
    public async Task ASecondLoadDestroysTheFrameAndReportsTheBlock()
    {
        string? reported = null;
        var cut = Render<ArtifactFrame>(parameters => parameters
            .Add(p => p.Title, "Interactive artifact")
            .Add(p => p.RenderUrl, TicketUrl)
            .Add(p => p.OnClosed, EventCallback.Factory.Create<string>(this, text => reported = text)));

        await cut.Find("iframe").LoadAsync(new ProgressEventArgs());
        cut.FindAll("iframe").Count.ShouldBe(1, "the first load is the document the frame was given");

        await cut.Find("iframe").LoadAsync(new ProgressEventArgs());

        cut.FindAll("iframe").ShouldBeEmpty("the frame is destroyed, not reloaded");
        cut.Find(".autonogrammer-artifact-frame-state").TextContent.ShouldContain("Navigation blocked");
        reported.ShouldNotBeNull();
        reported.ShouldContain("Navigation blocked");
    }

    /// <summary>A static view is labelled as a static view and carries its document in srcdoc under the same sandbox.</summary>
    [Fact]
    public void AStaticViewIsLabelledAndUsesSrcdoc()
    {
        var cut = Render<ArtifactFrame>(parameters => parameters
            .Add(p => p.Title, "Static artifact")
            .Add(p => p.SrcDoc, "<p>hello</p>")
            .Add(p => p.StaticView, true));

        cut.Markup.ShouldContain("Static view");
        var frame = cut.Find("iframe");
        frame.GetAttribute("srcdoc").ShouldBe("<p>hello</p>");
        frame.GetAttribute("sandbox").ShouldBe("allow-scripts");
        frame.HasAttribute("src").ShouldBeFalse();
    }

    /// <summary>When the sign-in ends, every frame is destroyed at once.</summary>
    [Fact]
    public void SigningOutDestroysTheFrame()
    {
        var session = new FakeAuthSession { HasToken = true };
        Services.AddSingleton<IAuthSession>(session);
        var cut = Render<ArtifactFrame>(parameters => parameters
            .Add(p => p.Title, "Interactive artifact")
            .Add(p => p.RenderUrl, TicketUrl));
        cut.FindAll("iframe").Count.ShouldBe(1);

        session.Clear();

        cut.WaitForAssertion(() => cut.FindAll("iframe").ShouldBeEmpty());
        cut.Markup.ShouldContain("signed out");
    }

    /// <summary>A head with no sign-in session shows the frame as usual, because only a signed-out tab must lose it.</summary>
    [Fact]
    public void WithoutASessionTheFrameStays()
    {
        var cut = Render<ArtifactFrame>(parameters => parameters
            .Add(p => p.Title, "Interactive artifact")
            .Add(p => p.RenderUrl, TicketUrl));

        cut.FindAll("iframe").Count.ShouldBe(1);
    }

    /// <summary>The artifact card names its artifact and opens the panel; it renders no content of the artifact.</summary>
    [Fact]
    public void TheCardOpensThePanelAndShowsNoContent()
    {
        ArtifactBlock? opened = null;
        var block = new ArtifactBlock
        {
            Id = "block-artifact-1",
            ArtifactId = "artifact-1",
            Version = 2,
            Title = "Quarterly figures",
            ArtifactKind = Autonogrammer.Contracts.Artifacts.ArtifactKind.Csv,
            Action = Autonogrammer.Contracts.Artifacts.ArtifactAction.Updated,
        };
        var cut = Render<ArtifactCard>(parameters => parameters
            .Add(p => p.Block, block)
            .Add(p => p.OnOpen, EventCallback.Factory.Create<ArtifactBlock>(this, card => opened = card)));

        cut.Find("button").Click();

        opened.ShouldBe(block);
        cut.Markup.ShouldContain("Quarterly figures");
        cut.Markup.ShouldContain("version 2");
        cut.FindAll("table").ShouldBeEmpty("the card never renders the content inline");
    }

    /// <summary>No artifact component posts a message to another window (D-23 forbids a message channel for the frame).</summary>
    [Fact]
    public void NoArtifactComponentPostsAMessage()
    {
        var folder = Path.Combine(CssSources.RepoRoot, "src", "Autonogrammer.UI", "Components", "Cowork", "Artifacts");
        Directory.GetFiles(folder).ShouldNotBeEmpty();
        foreach (var file in Directory.GetFiles(folder))
        {
            File.ReadAllText(file).ShouldNotContain("postMessage", Case.Insensitive, $"{Path.GetFileName(file)} must not post a message");
        }
    }

    /// <summary>The artifact stylesheet is imported by the entry file before the accessibility layer.</summary>
    [Fact]
    public void ArtifactsCssIsImportedBeforeTheAccessibilityLayer()
    {
        var entry = CssSources.ReadStyle("autonogrammer.css");
        entry.IndexOf("artifacts.css", StringComparison.Ordinal).ShouldBeGreaterThan(0);
        entry.IndexOf("artifacts.css", StringComparison.Ordinal).ShouldBeLessThan(entry.IndexOf("a11y.css", StringComparison.Ordinal));
    }

    private sealed class FakeAuthSession : IAuthSession
    {
        /// <inheritdoc />
        public event EventHandler? Changed;

        /// <inheritdoc />
        public bool HasToken { get; set; }

        /// <inheritdoc />
        public bool SessionEnded => false;

        /// <inheritdoc />
        public string? ReturnPath { get; set; }

        /// <inheritdoc />
        public void Clear()
        {
            HasToken = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
