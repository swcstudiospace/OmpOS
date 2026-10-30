using Autonogrammer.Presentation.About;

namespace Autonogrammer.Native.Tests.About;

/// <summary>An <see cref="IAboutClipboard"/> that records the texts it is given.</summary>
internal sealed class FakeAboutClipboard : IAboutClipboard
{
    /// <summary>Gets the texts written, oldest first.</summary>
    internal List<string> Texts { get; } = [];

    /// <inheritdoc/>
    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        Texts.Add(text);
        return Task.CompletedTask;
    }
}
