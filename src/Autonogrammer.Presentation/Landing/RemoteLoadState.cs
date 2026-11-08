namespace Autonogrammer.Presentation.Landing;

/// <summary>Whether the saved remote environments have been read.</summary>
public enum RemoteLoadState
{
    /// <summary>The list is being read.</summary>
    Loading = 0,

    /// <summary>The list was read, and may be empty.</summary>
    Loaded = 1,

    /// <summary>Reading the list threw. The card offers a retry.</summary>
    Failed = 2,
}
