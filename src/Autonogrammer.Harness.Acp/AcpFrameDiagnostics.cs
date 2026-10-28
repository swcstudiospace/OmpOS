using System.Text;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Acp;

/// <summary>Builds <see cref="MalformedFrameHarnessException"/> values that carry a bounded preview of the offending frame.</summary>
internal static class AcpFrameDiagnostics
{
    private const int PreviewBytes = 2048;

    /// <summary>Creates the exception for a frame that could not be decoded.</summary>
    /// <param name="frame">The offending frame.</param>
    /// <param name="detail">What was wrong with it.</param>
    /// <param name="inner">The underlying decode failure, if any.</param>
    /// <returns>The malformed-frame exception. Its message holds at most the first 2 KiB of the frame.</returns>
    public static MalformedFrameHarnessException Malformed(ReadOnlyMemory<byte> frame, string detail, Exception? inner = null)
    {
        var preview = Encoding.UTF8.GetString(frame.Span[..Math.Min(frame.Length, PreviewBytes)]);
        return new MalformedFrameHarnessException($"Malformed frame from omp acp ({detail}): {preview}", "acp", inner);
    }
}
