using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Tests.ModelPicker;
using OmpOS.Native.Tests.Parity;
using OmpOS.Native.Views;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Composer.Attachments;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>
/// Image attachments in the per-session runtime: a connected chat session wires them to the engine's answer and the selected model, an image-only message reaches the engine as image content, and the
/// Attach button shows only for a composer the session wired.
/// </summary>
public sealed class AgentSessionRuntimeImageTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static readonly ScriptedTurn Reply = new([new TextDeltaEvent("seen"), new CompletedEvent("end_turn")]);

    private static AgentSessionRuntime Create(IPromptChannel prompts, FakeModelControl? control = null, Func<string, VisionSupport>? vision = null)
    {
        const string sessionId = "session-1";
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary { Id = sessionId, Title = "Agent", Lifecycle = SessionLifecycle.Created, CreatedAt = DateTimeOffset.UnixEpoch, LastActivity = DateTimeOffset.UnixEpoch });
        using var model = new SessionCatalogModel(catalog, new ShellClock());
        model.Refresh();
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = model.Sessions.Single(),
            Prompts = prompts,
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            Models = control,
            ModelVision = vision,
        });
    }

    /// <summary>A connected chat session offers image attachments; a session with no engine does not.</summary>
    [Fact]
    public async Task OnlyAConnectedChatSessionOffersImages()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(() =>
        {
            var connected = Create(new ScriptedPromptChannel());
            var unconnected = Create(new UnconnectedPromptChannel());

            connected.Composer.Images.ShouldNotBeNull();
            connected.Composer.ImageSender.ShouldNotBeNull();
            unconnected.Composer.Images.ShouldBeNull("there is no engine to send an image to");
            unconnected.Composer.ImageSender.ShouldBeNull();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An image with no words is sent: the engine gets an empty text, the image with its type and bytes, the transcript shows the name, and the attachment is cleared.</summary>
    [Fact]
    public async Task AnImageOnlyMessageReachesTheEngineAsImageContent()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(Reply);
            var runtime = Create(channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var images = runtime.Composer.Images.ShouldNotBeNull();
            images.Add(PngHead, "image/png", "screen.png").Succeeded.ShouldBeTrue();

            await runtime.Composer.SendAsync().ConfigureAwait(true);
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);

            channel.Prompts.ShouldBe([string.Empty]);
            var sent = channel.Options.Single().Images.ShouldHaveSingleItem();
            sent.Name.ShouldBe("screen.png");
            sent.MimeType.ShouldBe("image/png");
            sent.Data.ToArray().ShouldBe(PngHead);
            images.HasItems.ShouldBeFalse("an accepted send clears the images");
            runtime.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldBe(["Attached image: screen.png"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Words and two images go together: the text is the prompt and both images are in the options, in attachment order.</summary>
    [Fact]
    public async Task WordsAndImagesTravelTogetherInAttachmentOrder()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(Reply);
            var runtime = Create(channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var images = runtime.Composer.Images.ShouldNotBeNull();
            _ = images.Add(PngHead, "image/png", "one.png");
            _ = images.Add([0xFF, 0xD8, 0xFF, 0xE0, 0, 16], "image/jpeg", "two.jpg");
            runtime.Composer.Draft = "compare these";

            await runtime.Composer.SendAsync().ConfigureAwait(true);
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);

            channel.Prompts.ShouldBe(["compare these"]);
            channel.Options.Single().Images.Select(static image => (image.Name, image.MimeType)).ShouldBe([("one.png", "image/png"), ("two.jpg", "image/jpeg")]);
            runtime.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldBe(["compare these\n\nAttached images: one.png, two.jpg"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The attachments follow the engine: unknown until it answers, refused when it says no images, accepted when it says yes, and the model's vision flag refuses a text-only model.</summary>
    [Fact]
    public async Task TheAttachmentsFollowTheEnginesAnswerAndTheSelectedModel()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(() =>
        {
            var control = new FakeModelControl();
            var runtime = Create(new ScriptedPromptChannel(), control, static id => string.Equals(id, "openai/gpt-x", StringComparison.Ordinal) ? VisionSupport.No : VisionSupport.Yes);
            var images = runtime.Composer.Images.ShouldNotBeNull();
            images.CanAttach.ShouldBeTrue("an engine that has not answered is allowed, with a caution");
            images.CapabilityNote.ShouldNotBeNullOrEmpty();

            control.SupportsImages = false;
            control.Report(ModelPickerFixture.Models, "anthropic/claude-sonnet");
            images.CanAttach.ShouldBeFalse("initialize said image false");
            images.DisabledReason.ShouldBe(AttachmentCopy.EngineRefuses);

            control.SupportsImages = true;
            control.Report(ModelPickerFixture.Models, "anthropic/claude-sonnet");
            images.CanAttach.ShouldBeTrue();
            images.CapabilityNote.ShouldBeEmpty("the model reads images");

            control.Report(ModelPickerFixture.Models, "openai/gpt-x");
            images.CanAttach.ShouldBeFalse("the selected model lists text input only");
            images.DisabledReason.ShouldBe(AttachmentCopy.ModelRefuses);
            images.Add(PngHead, "image/png", "a.png").Succeeded.ShouldBeFalse();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Attach button is on the composer the session wired and not on a bare composer.</summary>
    [Fact]
    public async Task TheAttachButtonShowsOnlyWhenTheSessionWiredTheComposer()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(() =>
        {
            var runtime = Create(new ScriptedPromptChannel());
            var wired = (ComposerView)ViewMapping.Create(runtime.Composer);
            var bare = (ComposerView)ViewMapping.Create(new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask));
            var wiredWindow = ViewHarness.Show(wired, width: 800, height: 600);
            var bareWindow = ViewHarness.Show(bare, width: 800, height: 600);
            try
            {
                wired.AttachButtonControl.IsVisible.ShouldBeTrue();
                wired.AttachButtonControl.IsEnabled.ShouldBeTrue();
                bare.AttachButtonControl.IsVisible.ShouldBeFalse();
                wired.FilePicker.ShouldNotBeNull();
                wired.ClipboardImages.ShouldNotBeNull();
            }
            finally
            {
                wiredWindow.Close();
                bareWindow.Close();
            }

            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
