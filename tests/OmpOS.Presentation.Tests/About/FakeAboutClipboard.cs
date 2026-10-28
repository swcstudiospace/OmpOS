using OmpOS.Presentation.About;

namespace OmpOS.Presentation.Tests.About;

/// <summary>An <see cref="IAboutClipboard"/> that records the text it is given, or refuses it like a window with no clipboard.</summary>
internal sealed class FakeAboutClipboard : IAboutClipboard
{
    /// <summary>Gets the texts written, oldest first.</summary>
    internal List<string> Texts { get; } = [];

    /// <summary>Gets or sets a value that makes every write fail.</summary>
    internal bool Refuse { get; set; }

    /// <inheritdoc/>
    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        if (Refuse)
        {
            throw new InvalidOperationException("No clipboard.");
        }

        Texts.Add(text);
        return Task.CompletedTask;
    }
}
