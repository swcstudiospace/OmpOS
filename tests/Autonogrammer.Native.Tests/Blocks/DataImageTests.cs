using Autonogrammer.Native.Views.Blocks;
using Avalonia.Media.Imaging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Blocks;

/// <summary>An inline image whose header is valid but whose decoder rejects it yields no picture instead of throwing.</summary>
public sealed class DataImageTests
{
    [Fact]
    public async Task ADecoderRejectionYieldsNoPicture()
    {
        var source = "data:image/png;base64," + Convert.ToBase64String(PngHeader());
        var calls = 0;
        Bitmap Reject(Stream stream)
        {
            calls++;
            throw new ArgumentException($"The decoder rejected {stream.Length} bytes.", nameof(stream));
        }

        await using var session = TestAppBuilder.StartSession();

        await session.Dispatch(() =>
        {
            DataImage.TryCreatePicture(source, Reject).ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        calls.ShouldBe(1);
    }

    /// <summary>A PNG signature and an IHDR chunk header. The header check reads only these bytes; the decoder is injected by the test.</summary>
    private static byte[] PngHeader() =>
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
