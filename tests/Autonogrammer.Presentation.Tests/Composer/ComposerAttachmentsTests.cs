using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Composer.Attachments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Composer;

/// <summary>Image attachments: magic-byte validation, limits, the capability gate and the send path.</summary>
public sealed class ComposerAttachmentsTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private sealed class FixedThumbnailer : IImageThumbnailer
    {
        public byte[]? CreateThumbnail(byte[] data, string mimeType, int maxEdge) => [9, (byte)maxEdge];
    }

    private static byte[] Png(int length)
    {
        var bytes = new byte[length];
        PngHead.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>Each allowed format is found by its leading bytes; anything else is not an image.</summary>
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 }, null)]
    [InlineData(new byte[] { 0x42, 0x4D, 0, 0 }, null)]
    [InlineData(new byte[] { 0x89, 0x50 }, null)]
    public void SnifferReadsMagicBytes(byte[] data, string? expected)
    {
        ImageSniffer.Detect(data).ShouldBe(expected, StringComparer.Ordinal);
    }

    /// <summary>A file named .png whose bytes are not an image is refused; a png with a wrong extension is accepted.</summary>
    [Fact]
    public void BytesDecideNotTheName()
    {
        var model = new ComposerAttachmentsModel();

        model.Add([1, 2, 3, 4], "image/png", "fake.png").Error.ShouldBe(AttachmentError.UnsupportedType);
        model.Add(Png(20), null, "photo.txt").Succeeded.ShouldBeTrue();
        model.Items[0].MimeType.ShouldBe("image/png");
        model.Add(Png(20), "text/plain", "x.png").Error.ShouldBe(AttachmentError.UnsupportedType);
    }

    /// <summary>The count limit, the single-size limit and the total limit each refuse with their own error and message.</summary>
    [Fact]
    public void LimitsRefuse()
    {
        var model = new ComposerAttachmentsModel(null, new AttachmentLimits(MaxCount: 3, MaxBytes: 100, MaxTotalBytes: 150));

        model.Add(Png(101), null, "a.png").Error.ShouldBe(AttachmentError.TooLarge);
        model.Notice.ShouldBe(AttachmentCopy.TooLarge(100));
        model.Add(Png(100), null, "a.png").Succeeded.ShouldBeTrue();
        model.Add(Png(60), null, "b.png").Error.ShouldBe(AttachmentError.TotalTooLarge);
        model.Add(Png(20), null, "c.png").Succeeded.ShouldBeTrue();
        model.Add(Png(10), null, "d.png").Succeeded.ShouldBeTrue();
        model.Add(Png(10), null, "e.png").Error.ShouldBe(AttachmentError.TooMany);
        model.Add([], null, "f.png").Error.ShouldBe(AttachmentError.TooMany);
        model.TotalBytes.ShouldBe(130);
        model.Items.Count.ShouldBe(3);
    }

    /// <summary>The documented defaults are eight images, 10 MiB each and 25 MiB together.</summary>
    [Fact]
    public void DefaultLimits()
    {
        var limits = new ComposerAttachmentsModel().Limits;

        limits.MaxCount.ShouldBe(8);
        limits.MaxBytes.ShouldBe(10L * 1024 * 1024);
        limits.MaxTotalBytes.ShouldBe(25L * 1024 * 1024);
    }

    /// <summary>The display name drops any path and control characters; the alt text and accessible names use it.</summary>
    [Fact]
    public void NamesAreCleanedAndAccessible()
    {
        var model = new ComposerAttachmentsModel(new FixedThumbnailer());

        var result = model.Add(Png(2048), null, "C:\\users\\me\\sh\tot.png");

        result.Attachment!.Name.ShouldBe("shot.png");
        result.Attachment.AltText.ShouldBe("shot.png");
        result.Attachment.AccessibleName.ShouldBe("Image shot.png, 2 KB");
        result.Attachment.RemoveName.ShouldBe("Remove image shot.png");
        result.Attachment.HasThumbnail.ShouldBeTrue();
        result.Attachment.Thumbnail.Span[1].ShouldBe((byte)64);
    }

    /// <summary>Remove drops one attachment; Clear drops all.</summary>
    [Fact]
    public void RemoveAndClear()
    {
        var model = new ComposerAttachmentsModel();
        var first = model.Add(Png(10), null, "a.png").Attachment!;
        model.Add(Png(10), null, "b.png");

        model.Remove(first).ShouldBeTrue();
        model.Remove(first).ShouldBeFalse();
        model.Items.Select(i => i.Name).ShouldBe(["b.png"]);
        model.Clear();
        model.HasItems.ShouldBeFalse();
    }

    /// <summary>The gate matrix: engine refusal wins, a known text-only model is refused, an unknown model is allowed with a note.</summary>
    [Theory]
    [InlineData(false, VisionSupport.Yes, false)]
    [InlineData(false, VisionSupport.Unknown, false)]
    [InlineData(true, VisionSupport.No, false)]
    [InlineData(null, VisionSupport.No, false)]
    [InlineData(true, VisionSupport.Yes, true)]
    [InlineData(null, VisionSupport.Yes, true)]
    [InlineData(null, VisionSupport.Unknown, true)]
    [InlineData(true, VisionSupport.Unknown, true)]
    public void GateMatrix(bool? engine, VisionSupport vision, bool allowed)
    {
        var decision = AttachmentGate.Evaluate(engine, vision);

        decision.CanAttach.ShouldBe(allowed);
        (decision.Reason.Length > 0).ShouldBe(!allowed);
        (decision.Note.Length > 0).ShouldBe(allowed && vision == VisionSupport.Unknown);
    }

    /// <summary>A refused gate stops Add and gives the reason; the model exposes the disabled state.</summary>
    [Fact]
    public void RefusedGateBlocksAdd()
    {
        var model = new ComposerAttachmentsModel();
        model.SetCapabilities(true, VisionSupport.No);

        var result = model.Add(Png(10), null, "a.png");

        result.Error.ShouldBe(AttachmentError.Refused);
        model.CanAttach.ShouldBeFalse();
        model.DisabledReason.ShouldBe(AttachmentCopy.ModelRefuses);
        model.HasItems.ShouldBeFalse();
    }

    /// <summary>A send with images goes through the image sender, may have blank text, and clears the images when accepted.</summary>
    [Fact]
    public async Task SendCarriesImagesAndClearsThem()
    {
        var images = new ComposerAttachmentsModel();
        var seen = new List<(string Text, int Count)>();
        var composer = new ComposerViewModel(static _ => Task.FromResult(false), static () => Task.CompletedTask)
        {
            Images = images,
            ImageSender = (text, _, items) =>
            {
                seen.Add((text, items.Count));
                return Task.FromResult(true);
            },
        };
        images.Add(Png(10), null, "a.png");

        await composer.SendAsync().ConfigureAwait(true);

        seen.ShouldBe([(string.Empty, 1)]);
        images.HasItems.ShouldBeFalse();
    }

    /// <summary>A refused send keeps the images; without an image sender a message with images is not sent at all.</summary>
    [Fact]
    public async Task RefusedSendKeepsImagesAndNoSenderNeverDropsThem()
    {
        var images = new ComposerAttachmentsModel();
        var plain = new List<string>();
        var composer = new ComposerViewModel(
            text =>
            {
                plain.Add(text);
                return Task.FromResult(true);
            },
            static () => Task.CompletedTask)
        {
            Images = images,
            Draft = "look",
        };
        images.Add(Png(10), null, "a.png");

        await composer.SendAsync().ConfigureAwait(true);

        plain.ShouldBeEmpty();
        images.HasItems.ShouldBeTrue();
        composer.ImageSender = (_, _, _) => Task.FromResult(false);
        await composer.SendAsync().ConfigureAwait(true);
        images.HasItems.ShouldBeTrue();
        composer.Draft.ShouldBe("look");
    }
}
