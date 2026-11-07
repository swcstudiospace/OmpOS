
namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Why a sensitive action asks. Empty means it does not ask.</summary>
public enum SensitivityReason
{
    /// <summary>No confirmation.</summary>
    None = 0,

    /// <summary>Credential title pattern.</summary>
    CredentialTitle = 1,

    /// <summary>File deletion.</summary>
    FileDeletion = 2,

    /// <summary>Purchase window.</summary>
    Purchase = 3,

    /// <summary>Mail or messaging raised above view.</summary>
    RaisedCategory = 4,

    /// <summary>Clipboard read or write.</summary>
    Clipboard = 5,

    /// <summary>First action in this application.</summary>
    FirstAction = 6,

    /// <summary>Dispatch-origin grant. Every action asks again.</summary>
    DispatchOrigin = 7,
}
