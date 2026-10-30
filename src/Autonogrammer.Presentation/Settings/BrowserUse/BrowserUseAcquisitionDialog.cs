using System.Globalization;

namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>
/// The confirmation shown before a Chromium download. <see cref="ConfirmAsync"/> is the only path that calls the source acquire operation.
/// Dismissing the dialog does not.
/// </summary>
public sealed class BrowserUseAcquisitionDialog
{
    private readonly Func<CancellationToken, Task> _acquire;

    /// <summary>Initializes a dialog over one manifest row.</summary>
    /// <param name="offer">The row the dialog names.</param>
    /// <param name="acquire">The source acquire operation. Invoked only by <see cref="ConfirmAsync"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BrowserUseAcquisitionDialog(BrowserUseManifestOffer offer, Func<CancellationToken, Task> acquire)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(acquire);
        Version = offer.Version;
        SourceHost = offer.SourceHost;
        SizeBytes = offer.SizeBytes;
        LicenceLine = offer.LicenceLine;
        _acquire = acquire;
    }

    /// <summary>Gets the Chromium version.</summary>
    public string Version { get; }

    /// <summary>Gets the archive URL host.</summary>
    public string SourceHost { get; }

    /// <summary>Gets the archive size in bytes.</summary>
    public long SizeBytes { get; }

    /// <summary>Gets the licence line.</summary>
    public string LicenceLine { get; }

    /// <summary>Gets the sentence that names version, source host, size and licence line.</summary>
    public string Text =>
        Version
        + " from "
        + SourceHost
        + ", "
        + SizeBytes.ToString(CultureInfo.InvariantCulture)
        + " bytes. "
        + LicenceLine;

    /// <summary>Gets a value indicating whether the user confirmed.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>Confirms the dialog and acquires the archive. This is the only caller of the source acquire operation.</summary>
    /// <param name="cancellationToken">Cancels the acquisition.</param>
    /// <returns>A task that completes when the acquisition finishes.</returns>
    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        IsConfirmed = true;
        await _acquire(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closes the dialog without acquiring.</summary>
    public void Dismiss() => IsConfirmed = false;
}
