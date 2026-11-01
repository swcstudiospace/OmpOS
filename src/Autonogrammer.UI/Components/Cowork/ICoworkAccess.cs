namespace Autonogrammer.UI.Components.Cowork;

/// <summary>
/// Whether this head may show Cowork. The Web head answers from the auth mode and the held session; a test answers with a fake.
/// The component reads this port. It never reads a browser storage API.
/// </summary>
public interface ICoworkAccess
{
    /// <summary>Occurs after availability, the sign-in offer or a load failure changes.</summary>
    event EventHandler? Changed;

    /// <summary>Gets whether the auth answer has arrived. Until then Cowork is not available.</summary>
    bool IsResolved { get; }

    /// <summary>Gets whether Cowork may be selected. A Demo head is never available.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets whether the signed-out state may link to the sign-in route. The Demo has no such route.</summary>
    bool OffersSignIn { get; }

    /// <summary>Gets whether the last session-list load failed. A head that owns the catalog may ignore this and read the catalog instead.</summary>
    bool LoadFailed { get; }

    /// <summary>Loads the session list again.</summary>
    /// <returns>A task that completes when the reload has been requested.</returns>
    Task RetryAsync();
}
