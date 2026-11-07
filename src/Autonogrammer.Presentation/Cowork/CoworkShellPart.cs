namespace Autonogrammer.Presentation.Cowork;

/// <summary>One region of the Cowork layout. There is no board region.</summary>
public enum CoworkShellPart
{
    /// <summary>The left rail: tasks, projects and the filter.</summary>
    Rail,

    /// <summary>The conversation. Phase 70 fills the transcript.</summary>
    Conversation,

    /// <summary>The right dock.</summary>
    Dock,
}
