using Autonogrammer.Contracts.Controls;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Composer.Attachments;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>Cowork conversation composition, image refusal and attachment limits (AG-70-4, AG-70-11, D-28, D-29).</summary>
public sealed class CoworkConversationModelTests
{
    /// <summary>The model composes the real session and composer types, the approval queue and the three provider states.</summary>
    [Fact]
    public void ComposesSessionComposerApprovalsAndProviderChips()
    {
        var session = new SessionViewModel();
        var composer = new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask);
        var queue = new CoworkApprovalQueue();
        var approval = new PendingToolApproval
        {
            RequestId = "req-1",
            ToolName = "read_file",
            RequestedAt = DateTimeOffset.UnixEpoch,
            GrantableTools = ["read_file"],
        };
        queue.Add(approval, "workspace/notes.md", "host observed read", "model said run this");
        var providers = new[]
        {
            new ProviderStatusChip("Local", ProviderChipState.Available),
            new ProviderStatusChip("Cloud", ProviderChipState.NeedsSetup),
            new ProviderStatusChip("Remote", ProviderChipState.Unavailable, "no key"),
        };

        var model = new CoworkConversationModel(session, composer, queue, providers);

        model.Session.ShouldBeSameAs(session);
        model.Composer.ShouldBeSameAs(composer);
        model.Approvals.Cards.Single().Approval.ShouldBeSameAs(approval);
        model.Approvals.Cards.Single().Approval.ShouldBeOfType<PendingToolApproval>();
        model.Providers.Select(chip => chip.State).ShouldBe([ProviderChipState.Available, ProviderChipState.NeedsSetup, ProviderChipState.Unavailable]);
        model.Providers[1].Label.ShouldBe(ProviderStatusChip.NeedsSetupLabel);
        model.Providers[2].Reason.ShouldBe("no key");
        model.Providers[2].Label.ShouldBe(ProviderStatusChip.UnavailableLabel);
    }

    /// <summary>A model that reads no images disables image tools and image attachments, and the refusal can be cleared in place.</summary>
    [Fact]
    public void AModelThatReadsNoImagesDisablesImageToolsAndCanBeChangedInPlace()
    {
        var model = Create(readsImages: false);
        var chip = model.AddAttachment("shot.png", isImage: true, bytes: 12, exists: true);

        model.ImageToolsEnabled.ShouldBeFalse();
        model.CanAttachImages.ShouldBeFalse();
        model.ImageToolMessage.ShouldBe(CoworkConversationModel.ModelCannotReadImages);
        chip.Reason.ShouldBe(CoworkConversationModel.ModelCannotReadImages);
        chip.BlocksSend.ShouldBeTrue();
        model.CanSend.ShouldBeFalse();
        string.Equals(model.ImageToolMessage, AttachmentCopy.ModelRefuses, StringComparison.Ordinal).ShouldBeFalse();
        string.Equals(chip.Reason, AttachmentCopy.ModelRefuses, StringComparison.Ordinal).ShouldBeFalse();

        model.ReadsImages = true;

        model.ImageToolsEnabled.ShouldBeTrue();
        model.ImageToolMessage.ShouldBeNull();
        chip.Reason.ShouldBeNull();
        model.CanSend.ShouldBeTrue();
    }

    /// <summary>An attachment over the image limits, or a missing file, shows the reason and blocks send. Remove clears the block.</summary>
    [Fact]
    public async Task OverLimitAndMissingFileBlockSendUntilRemoved()
    {
        var sent = 0;
        var model = new CoworkConversationModel(
            new SessionViewModel(),
            new ComposerViewModel(_ => { sent++; return Task.FromResult(true); }, static () => Task.CompletedTask));
        model.Composer.Draft = "hello";
        var over = model.AddAttachment("huge.png", isImage: true, bytes: CoworkConversationModel.ImageLimits.MaxBytes + 1, exists: true);
        var missing = model.AddAttachment("gone.txt", isImage: false, bytes: 4, exists: false);

        CoworkConversationModel.ImageLimits.MaxCount.ShouldBe(8);
        CoworkConversationModel.ImageLimits.MaxBytes.ShouldBe(10L * 1024 * 1024);
        CoworkConversationModel.ImageLimits.MaxTotalBytes.ShouldBe(25L * 1024 * 1024);
        over.Reason.ShouldBe(AttachmentCopy.TooLarge(CoworkConversationModel.ImageLimits.MaxBytes));
        missing.Reason.ShouldBe(CoworkConversationModel.MissingFile);
        model.CanSend.ShouldBeFalse();
        (await model.TrySendAsync()).ShouldBeFalse();
        sent.ShouldBe(0);

        model.RemoveAttachment(over.Id).ShouldBeTrue();
        model.RemoveAttachment(missing.Id).ShouldBeTrue();
        model.Attachments.ShouldBeEmpty();
        model.CanSend.ShouldBeTrue();
        (await model.TrySendAsync()).ShouldBeTrue();
        sent.ShouldBe(1);
    }

    private static CoworkConversationModel Create(bool readsImages) =>
        new(
            new SessionViewModel(),
            new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask),
            readsImages: readsImages);
}
