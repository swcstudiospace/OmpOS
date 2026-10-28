namespace Autonogrammer.Presentation.Composer;

/// <summary>How full the context window is. The state is shown as words as well as colour.</summary>
public enum ContextRingState
{
    /// <summary>The window size is not known, so no percentage can be given.</summary>
    Unknown = 0,

    /// <summary>Under 70 percent of the window is used.</summary>
    Normal = 1,

    /// <summary>70 percent or more of the window is used: the conversation is getting long.</summary>
    Warning = 2,

    /// <summary>90 percent or more of the window is used: the engine will soon have to summarise or drop history.</summary>
    Critical = 3,
}
