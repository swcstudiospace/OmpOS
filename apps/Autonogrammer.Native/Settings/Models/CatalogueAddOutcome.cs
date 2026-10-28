namespace Autonogrammer.Native.Settings.Models;

/// <summary>The typed result of adding a catalogue listing to omp. It never holds a key.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">The reason for a result other than <see cref="CatalogueAddStatus.Added"/> and <see cref="CatalogueAddStatus.Updated"/>, worded for the user and free of paths; null for a success.</param>
internal sealed record CatalogueAddOutcome(CatalogueAddStatus Status, string? Message = null)
{
    /// <summary>Gets a value indicating whether omp now uses the provider.</summary>
    public bool IsSuccess => Status is CatalogueAddStatus.Added or CatalogueAddStatus.Updated;

    /// <summary>Creates an outcome that is not a success.</summary>
    /// <param name="status">What happened.</param>
    /// <param name="message">The reason, worded for the user.</param>
    /// <returns>The outcome.</returns>
    public static CatalogueAddOutcome Refused(CatalogueAddStatus status, string message) => new(status, message);
}
