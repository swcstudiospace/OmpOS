namespace OmpOS.Presentation.Settings;

/// <summary>A section (a left-navigation page) of the settings window.</summary>
public enum SettingsSection
{
    /// <summary>Idle stop and retention.</summary>
    Runtime = 0,

    /// <summary>The model providers omp uses.</summary>
    Models = 1,

    /// <summary>Motion and the privacy statement.</summary>
    General = 2,

    /// <summary>The terminal auto-run switch and the approval policy note.</summary>
    Agents = 3,

    /// <summary>The read-only status of inline completion.</summary>
    Tab = 4,

    /// <summary>The four plugins connected to every omp process, read-only.</summary>
    Plugins = 5,

    /// <summary>Project and user rules.</summary>
    Rules = 6,

    /// <summary>Project skills.</summary>
    Skills = 7,

    /// <summary>The slash command catalogue.</summary>
    Commands = 8,
}
