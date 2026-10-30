namespace Autonogrammer.Cowork.Tools;

/// <summary>The permission mode the default gate reads. Auto follows the same rules as Manual for provider calls.</summary>
public enum CoworkPermissionMode
{
    /// <summary>Ask unless a stored grant covers a non-destructive ByPolicy call.</summary>
    Manual = 0,

    /// <summary>The same as <see cref="Manual"/> for provider calls.</summary>
    Auto = 1,

    /// <summary>Allow ByPolicy calls until the session is tainted.</summary>
    Skip = 2,
}
