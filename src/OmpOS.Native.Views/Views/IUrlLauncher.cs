namespace OmpOS.Native.Views;

/// <summary>Opens an address in the system browser. The About window opens its links through this seam, so tests record the address instead of starting a browser.</summary>
public interface IUrlLauncher
{
    /// <summary>Opens an address.</summary>
    /// <param name="uri">The absolute address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the system accepted the address; <see langword="false"/> when no browser could be started.</returns>
    Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken);
}
