using Autonogrammer.Native.Views.Cowork.Artifacts;
using Avalonia.Media.Imaging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>An artifact image is refused, with its code, when the size cap is exceeded or the decoder rejects a header-valid body.</summary>
public sealed class ArtifactImageViewTests
{
    /// <summary>A header-valid PNG that the decoder rejects is refused as undecodable, not thrown.</summary>
    [Fact]
    public async Task ADecoderRejectionIsRefusedAsUndecodable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var view = new ArtifactImageView();

            view.Show("chart", "image/png", PngHeaderWithoutImageData(), maxImageBytes: 1024, decode: Reject);

            view.PictureControl.Source.ShouldBeNull();
            view.PictureControl.IsVisible.ShouldBeFalse();
            view.MessageText.ShouldContain("(undecodable)", Case.Sensitive);
            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    /// <summary>The configured cap is the one applied: an image over it is refused as too large.</summary>
    [Fact]
    public async Task AnImageOverTheConfiguredCapIsRefusedAsTooLarge()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var view = new ArtifactImageView();

            view.Show("chart", "image/png", PngHeaderWithoutImageData(), maxImageBytes: 10);

            view.PictureControl.IsVisible.ShouldBeFalse();
            view.MessageText.ShouldContain("(too_large)", Case.Sensitive);
            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    private static Bitmap Reject(Stream stream) => throw new ArgumentException($"The decoder rejected {stream.Length} bytes.", nameof(stream));

    /// <summary>A PNG signature and an IHDR chunk, which pass the header validation. The decoder is injected by the tests that need one.</summary>
    private static byte[] PngHeaderWithoutImageData() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        .. "IHDR"u8.ToArray(),
        0x00, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    ];
}
