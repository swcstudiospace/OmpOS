using Autonogrammer.Contracts.Controls;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Composer.Attachments;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.State;
using Autonogrammer.UI.Components.Cowork;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork;

/// <summary>bUnit accessibility of the Cowork conversation components (AG-70-11, D-37).</summary>
public sealed class CoworkConversationTests : UiTestContext
{
    /// <summary>Cards, chips, remove, focus order and the image alt text render from the presentation model.</summary>
    [Fact]
    public void RendersChipsCardsAndFocusOrder()
    {
        var session = new SessionViewModel();
        session.ApplyEvent(new StreamEvent
        {
            Seq = 1,
            SessionId = "s",
            CorrelationId = "c",
            Timestamp = DateTimeOffset.UnixEpoch,
            Payload = new UiBlockPayload
            {
                Block = new ImageBlock
                {
                    Id = "call-1-image-0",
                    CallId = "call-1",
                    ToolName = "read",
                    MimeType = "image/png",
                    DataBase64 = string.Empty,
                    Description = "A desk lamp",
                    ByteLength = 0,
                    Omitted = true,
                },
            },
        });
        var queue = new CoworkApprovalQueue();
        queue.Add(new PendingToolApproval
        {
            RequestId = "req-1",
            ToolName = "read_file",
            RequestedAt = DateTimeOffset.UnixEpoch,
            GrantableTools = ["read_file"],
        }, "workspace/notes.md", "host observed read", "MODEL-INSTRUCTION-SECRET");
        var model = new CoworkConversationModel(
            session,
            new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask),
            queue,
            [
                new ProviderStatusChip("Local", ProviderChipState.Available),
                new ProviderStatusChip("Cloud", ProviderChipState.NeedsSetup),
            ],
            readsImages: false,
            motion: MotionPreference.Reduced);
        var chip = model.AddAttachment("shot.png", isImage: true, bytes: 8, exists: true);

        var cut = Render<CoworkConversation>(p => p.Add(c => c.Model, model));
        AssertRegions(cut);
        cut.Find("[aria-label='Remove shot.png']").Click();
        model.Attachments.ShouldBeEmpty();
        chip.Id.ShouldNotBeNullOrWhiteSpace();
        cut.Markup.ShouldContain("A desk lamp");
        cut.Markup.ShouldContain("workspace/notes.md");
        cut.Markup.ShouldContain("host observed read");
        cut.Markup.ShouldNotContain("MODEL-INSTRUCTION-SECRET");
        cut.Markup.ShouldContain(CoworkConversationModel.ModelCannotReadImages);
        cut.Markup.ShouldNotContain(AttachmentCopy.ModelRefuses);
        cut.Find("[aria-label='Send']").ShouldNotBeNull();
        cut.Find("[aria-label='Retry']").ShouldNotBeNull();
        cut.Find("[aria-label='Steer']").ShouldNotBeNull();
    }

    private static void AssertRegions(IRenderedComponent<CoworkConversation> cut)
    {
        cut.Find(".autonogrammer-cowork-chat").ClassList.ShouldContain("reduced-motion", StringComparer.Ordinal);
        var regions = cut.FindAll("[role=region]").Select(static region => region.GetAttribute("aria-label")).ToList();
        regions.ShouldBe([CoworkConversationModel.TranscriptName, CoworkConversationModel.ComposerName, CoworkConversationModel.ApprovalsName]);
        cut.Find("[aria-label='Transcript']").GetAttribute("tabindex").ShouldBe("0");
        cut.Find("[aria-label='Composer']").GetAttribute("tabindex").ShouldBe("0");
        cut.Find("[aria-label='Approvals']").GetAttribute("tabindex").ShouldBe("0");
        cut.Markup.IndexOf("aria-label=\"Transcript\"", StringComparison.Ordinal).ShouldBeLessThan(cut.Markup.IndexOf("aria-label=\"Composer\"", StringComparison.Ordinal));
        cut.Markup.IndexOf("aria-label=\"Composer\"", StringComparison.Ordinal).ShouldBeLessThan(cut.Markup.IndexOf("aria-label=\"Approvals\"", StringComparison.Ordinal));
    }

    /// <summary>The stylesheet is tokens only, has no motion rules, sizes the remove control, and is imported once before a11y.css.</summary>
    [Fact]
    public void StylesheetUsesTokensAndIsImportedBeforeA11y()
    {
        var css = CssSources.StripComments(CssSources.ReadStyle("cowork-chat.css"));
        css.ShouldNotContain("transition");
        css.ShouldNotContain("animation");
        css.ShouldNotContain("@keyframes");
        css.ShouldNotContain("#");
        css.ShouldContain("min-block-size: var(--autonogrammer-target-min)");
        css.ShouldContain("min-inline-size: var(--autonogrammer-target-min)");
        var entry = CssSources.ReadStyle("autonogrammer.css");
        var chat = entry.IndexOf("cowork-chat.css", StringComparison.Ordinal);
        var a11y = entry.IndexOf("a11y.css", StringComparison.Ordinal);
        chat.ShouldBeGreaterThan(0);
        chat.ShouldBeLessThan(a11y);
        entry.Split("cowork-chat.css").Length.ShouldBe(2);
    }

    /// <summary>The three components declare no page and inject no auth, API, Cowork or Harness type.</summary>
    [Fact]
    public void ComponentsStayOnContractsAndPresentation()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Autonogrammer.UI", "Components", "Cowork");
        foreach (var name in new[] { "CoworkConversation.razor", "CoworkComposer.razor", "CoworkApprovalCard.razor" })
        {
            var text = File.ReadAllText(Path.Combine(root, name));
            text.ShouldNotContain("@page");
            text.ShouldNotContain("IAuthSession");
            text.ShouldNotContain("IAuthConfigSource");
            text.ShouldNotContain("Autonogrammer.Cowork");
            text.ShouldNotContain("Autonogrammer.Harness");
            text.ShouldNotContain("@inject");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Autonogrammer.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
