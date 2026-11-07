namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Head-provided clipboard. Backends do not implement operating-system clipboard protocols when the head supplies this port.</summary>
public interface IClipboardTextPort
{
    /// <summary>Reads clipboard text.</summary>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The text, or <see langword="null"/> when the clipboard has none.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Writes clipboard text.</summary>
    /// <param name="text">The text to write.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>A completed task.</returns>
    ValueTask WriteAsync(string text, CancellationToken cancellationToken);
}

