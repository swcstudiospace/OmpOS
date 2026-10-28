using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>A terminal output sink that drops everything.</summary>
internal sealed class DiscardingOutputSink : IProcessOutputSink
{
    /// <inheritdoc />
    public void Write(string text)
    {
    }
}
