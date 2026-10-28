using OmpOS.Presentation.About;

namespace OmpOS.Presentation.Tests.About;

/// <summary>An <see cref="IAboutClipboard"/> whose write is cancelled.</summary>
internal sealed class CancellingClipboard : IAboutClipboard
{
    /// <inheritdoc/>
    public Task SetTextAsync(string text, CancellationToken cancellationToken) => throw new OperationCanceledException(cancellationToken);
}
