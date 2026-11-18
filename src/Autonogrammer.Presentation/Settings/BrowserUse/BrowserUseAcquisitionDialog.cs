using System.Globalization;

namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>
/// The confirmation shown before a Chromium download. <see cref="ConfirmAsync"/> is the only path that calls the source acquire operation.
/// Dismissing the dialog does not start one; dismissing while one runs stops it.
/// </summary>
public sealed class BrowserUseAcquisitionDialog
{
    private readonly Func<CancellationToken, Task> _acquire;
    private CancellationTokenSource? _acquisition;

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

    /// <summary>Gets the sentence that says why the last acquisition was refused, or null.</summary>
    public string? ErrorText { get; private set; }

    /// <summary>
    /// Confirms the dialog and acquires the archive. This is the only caller of the source acquire operation. A confirm while an acquisition
    /// runs does nothing. A refused acquisition sets <see cref="ErrorText"/> and leaves the dialog open so the user can confirm again; an
    /// acquisition that <see cref="Dismiss"/> stopped ends without an error.
    /// </summary>
    /// <param name="cancellationToken">Cancels the acquisition.</param>
    /// <returns>A task that completes when the acquisition finishes, fails or is stopped.</returns>
    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (_acquisition is not null)
        {
            return;
        }

        var acquisition = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acquisition = acquisition;
        ErrorText = null;
        try
        {
            await _acquire(acquisition.Token).ConfigureAwait(true);
        }
        catch (SettingsSourceException exception)
        {
            ErrorText = exception.Message;
        }
        catch (OperationCanceledException) when (acquisition.IsCancellationRequested)
        {
            // Dismissed while the download ran: there is nothing to report.
        }
        finally
        {
            _acquisition = null;
            acquisition.Dispose();
        }
    }

    /// <summary>Closes the dialog. An acquisition that is running is stopped; none is started.</summary>
    public void Dismiss() => _acquisition?.Cancel();
}
