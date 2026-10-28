namespace Autonogrammer.Presentation.Settings.Customize;

/// <summary>Where an entry of a Customize catalogue comes from.</summary>
public enum CustomizeScope
{
    /// <summary>The user's own files, shared by every workspace.</summary>
    User = 0,

    /// <summary>A file of the open workspace.</summary>
    Project = 1,

    /// <summary>Built into Autonogrammer.</summary>
    BuiltIn = 2,
}
