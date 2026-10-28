using System.Text;

namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// Collects the output a runner streams to it, and optionally runs a callback on each piece.
/// </summary>
internal sealed class RecordingSink(Action<string>? onWrite = null) : IProcessOutputSink
{
    private readonly Lock _sync = new();
    private readonly StringBuilder _text = new();

    /// <summary>Every piece of output received so far, in order.</summary>
    public string Text
    {
        get
        {
            lock (_sync)
            {
                return _text.ToString();
            }
        }
    }

    /// <inheritdoc />
    public void Write(string text)
    {
        lock (_sync)
        {
            _text.Append(text);
        }

        onWrite?.Invoke(text);
    }
}
