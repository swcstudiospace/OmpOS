namespace Autonogrammer.Native.Settings.Models;

/// <summary>What happened when a catalogue listing was added to the workspace.</summary>
internal enum CatalogueAddStatus
{
    /// <summary>The engine now has a provider it did not have: a credential was imported or a new <c>models.yml</c> block was written.</summary>
    Added = 0,

    /// <summary>The engine already had the provider and now uses the new route, base URL, models or key.</summary>
    Updated,

    /// <summary>A base URL placeholder is missing or is not a plain identifier, so no address was built.</summary>
    InvalidUrlVariable,

    /// <summary>The route takes a key and the request held none, and no key is stored for the block yet.</summary>
    KeyMissing,

    /// <summary>The listing is a coding plan and the user did not confirm that they read the vendor's rule text.</summary>
    PlanRuleNotAccepted,

    /// <summary>The request names a listing or route the catalogue does not have, or a value that cannot be written (a key with a space, no model id).</summary>
    InvalidRequest,

    /// <summary>The engine read the change and refused it, or the id belongs to a provider Autonogrammer does not own. The reason is in the outcome.</summary>
    EngineRejected,

    /// <summary>The change could not be tried: the listing is a sign-in row, the engine could not be run, or a file or the key store could not be written.</summary>
    Unavailable,
}
