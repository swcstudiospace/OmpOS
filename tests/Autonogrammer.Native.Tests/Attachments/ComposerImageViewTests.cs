using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Attachments;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Composer.Attachments;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Attachments;

/// <summary>Image input in the composer view: the Attach button, paste, the dropped-file path, the preview strip, removal by button and by Delete, and the disabled state.</summary>
public sealed class ComposerImageViewTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private sealed class FakePicker : IFilePicker
    {
        internal int Calls { get; private set; }

        internal long LastLimit { get; private set; }

        public Task<IReadOnlyList<PickedImage>> PickImagesAsync(long maxBytesPerFile, CancellationToken cancellationToken)
        {
            Calls++;
            LastLimit = maxBytesPerFile;
            return Task.FromResult<IReadOnlyList<PickedImage>>([new PickedImage("C:\\pics\\one.png", PngHead, "image/png"), new PickedImage("notes.png", [1, 2, 3], "image/png")]);
        }
    }

    private sealed class FakeClipboard : IClipboardImageSource
    {
        internal PickedImage? Image { get; set; }

        public Task<PickedImage?> TryGetImageAsync(CancellationToken cancellationToken) => Task.FromResult(Image);
    }

    private sealed class Rig : IDisposable
    {
        internal Rig(ComposerView view, Window window, ComposerViewModel composer, ComposerAttachmentsModel? images, FakePicker picker, FakeClipboard clipboard)
        {
            View = view;
            Window = window;
            Composer = composer;
            Images = images;
            Picker = picker;
            Clipboard = clipboard;
        }

        internal ComposerView View { get; }

        internal Window Window { get; }

        internal ComposerViewModel Composer { get; }

        internal ComposerAttachmentsModel? Images { get; }

        internal FakePicker Picker { get; }

        internal FakeClipboard Clipboard { get; }

        public void Dispose() => Window.Close();
    }

    private static Rig Open(bool withImages = true)
    {
        var images = withImages ? new ComposerAttachmentsModel() : null;
        var composer = new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask) { Images = images };
        var picker = new FakePicker();
        var clipboard = new FakeClipboard();
        var view = new ComposerView { DataContext = composer, FilePicker = picker, ClipboardImages = clipboard };
        var window = ViewHarness.Show(view, width: 800, height: 600);
        return new Rig(view, window, composer, images, picker, clipboard);
    }

    private static async Task SettleWorkAsync(Rig rig)
    {
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSettled(object? sender, EventArgs e) => settled.TrySetResult();
        rig.View.WorkSettled += OnSettled;
        try
        {
            if (rig.View.IsWorking)
            {
                await settled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            rig.View.WorkSettled -= OnSettled;
        }

        ViewHarness.Settle(rig.Window);
    }

    /// <summary>Without an attachments model there is no Attach button and no strip, so the composer's keyboard order is unchanged.</summary>
    [Fact]
    public async Task NothingShowsWithoutTheModel()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open(withImages: false);

            rig.View.AttachButtonControl.IsVisible.ShouldBeFalse();
            rig.View.ImageStripHost.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Attach button is a named tab stop of target size; Enter opens the picker, the files are validated, and the strip lists the accepted one with its name and a notice for the refused one.</summary>
    [Fact]
    public async Task AttachButtonPicksAndTheStripListsTheImages()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            var attach = rig.View.AttachButtonControl;
            attach.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(attach).ShouldBe(AttachmentCopy.AttachName);
            ViewHarness.TabOrder(rig.View).ShouldContain(AttachmentCopy.AttachName, StringComparer.Ordinal);

            ViewHarness.Click(rig.Window, attach);
            await SettleWorkAsync(rig).ConfigureAwait(true);

            rig.Picker.Calls.ShouldBe(1);
            rig.Picker.LastLimit.ShouldBe(rig.Images!.Limits.MaxBytes);
            rig.Images.Items.Select(i => i.Name).ShouldBe(["one.png"]);
            rig.View.ImageStripHost.IsVisible.ShouldBeTrue();
            rig.View.ImageNoticeText.Text.ShouldBe(AttachmentCopy.Unsupported);
            ViewHarness.TabOrder(rig.View).ShouldContain("Image one.png, 12 B", StringComparer.Ordinal);
            ViewHarness.Unnamed(rig.View).ShouldBeEmpty();
            AutomationProperties.GetName(rig.View.ImageStripList).ShouldBe(AttachmentCopy.StripName);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ctrl+V with an image on the clipboard attaches it, and Ctrl+V with none leaves the strip alone.</summary>
    [Fact]
    public async Task PasteAddsAClipboardImage()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var rig = Open();
            ViewHarness.Find<TextBox>(rig.View, "PromptInput").Focus();

            ViewHarness.Press(rig.Window, Key.V, PhysicalKey.V, RawInputModifiers.Control);
            await SettleWorkAsync(rig).ConfigureAwait(true);
            rig.Images!.HasItems.ShouldBeFalse();

            rig.Clipboard.Image = new PickedImage("pasted-image.png", PngHead, "image/png");
            ViewHarness.Press(rig.Window, Key.V, PhysicalKey.V, RawInputModifiers.Control);
            await SettleWorkAsync(rig).ConfigureAwait(true);

            rig.Images.Items.Select(i => i.Name).ShouldBe(["pasted-image.png"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Dropped files go through the same validation as the picker.</summary>
    [Fact]
    public async Task DroppedFilesAreValidatedLikePickedOnes()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();

            var added = rig.View.AddImages([new PickedImage("a.png", PngHead, null), new PickedImage("b.exe", [0x4D, 0x5A, 0, 0], null)]);

            added.ShouldBe(1);
            rig.Images!.Items.Count.ShouldBe(1);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The remove button and the Delete key on a focused thumbnail each remove one image, and focus returns to the prompt.</summary>
    [Fact]
    public async Task RemoveByButtonAndByDeleteKey()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();
            rig.View.AddImages([new PickedImage("a.png", PngHead, null), new PickedImage("b.png", PngHead, null)]);
            ViewHarness.Settle(rig.Window);

            var thumb = rig.View.ImageStripList.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b)?.StartsWith("Image a.png", StringComparison.Ordinal) == true);
            thumb.Focus();
            ViewHarness.Press(rig.Window, Key.Delete, PhysicalKey.Delete);
            rig.Images!.Items.Select(i => i.Name).ShouldBe(["b.png"]);

            var remove = rig.View.ImageStripList.GetVisualDescendants().OfType<Button>().First(b => string.Equals(AutomationProperties.GetName(b), "Remove image b.png", StringComparison.Ordinal));
            remove.MinHeight.ShouldBeGreaterThanOrEqualTo(32);
            ViewHarness.Click(rig.Window, remove);
            rig.Images.HasItems.ShouldBeFalse();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A model that does not read images disables Attach and states the reason in the live region; an unknown model allows it with the caution once an image is attached.</summary>
    [Fact]
    public async Task DisabledStateStatesTheReason()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            using var rig = Open();

            rig.Images!.SetCapabilities(true, VisionSupport.No);
            ViewHarness.Settle(rig.Window);
            rig.View.AttachButtonControl.IsEnabled.ShouldBeFalse();
            rig.View.ImageNoticeText.Text.ShouldBe(AttachmentCopy.ModelRefuses);
            AutomationProperties.GetLiveSetting(rig.View.ImageNoticeText).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetHelpText(rig.View.AttachButtonControl).ShouldBe(AttachmentCopy.ModelRefuses);

            rig.Images.SetCapabilities(null, VisionSupport.Unknown);
            rig.View.AddImages([new PickedImage("a.png", PngHead, null)]);
            ViewHarness.Settle(rig.Window);
            rig.View.AttachButtonControl.IsEnabled.ShouldBeTrue();
            rig.View.ImageNoticeText.Text.ShouldBe(AttachmentCopy.ModelMayIgnore);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
