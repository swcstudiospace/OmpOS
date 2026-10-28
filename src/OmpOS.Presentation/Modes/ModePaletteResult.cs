namespace OmpOS.Presentation.Modes;

/// <summary>What accepting a skill in the slash palette did.</summary>
public enum ModePaletteResult
{
    /// <summary>The key was a plain accept, so the head inserts the skill's slash token as it always did.</summary>
    InsertSkillToken = 0,

    /// <summary>The key was Alt+Enter: the skill is now the session's mode and nothing is inserted.</summary>
    ModeActivated = 1,

    /// <summary>The skill could not be pinned; the status says why.</summary>
    Failed = 2,
}
