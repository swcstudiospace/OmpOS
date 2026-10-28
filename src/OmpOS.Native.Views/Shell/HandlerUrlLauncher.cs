namespace OmpOS.Native.Views.Shell;

/// <summary>Gives the slash handlers the head's system-browser launcher.</summary>
/// <param name="inner">The head's launcher.</param>
internal sealed class HandlerUrlLauncher(IUrlLauncher inner) : OmpOS.Presentation.Commands.Handlers.IUrlLauncher
{
    /// <inheritdoc/>
    public Task<bool> OpenAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        return inner.OpenAsync(url, cancellationToken);
    }
}
