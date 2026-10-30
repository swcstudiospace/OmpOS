namespace Autonogrammer.Cowork.Abstractions;

/// <summary>What a tool does to the world outside the model.</summary>
public enum CoworkToolEffect
{
    /// <summary>The tool only reads.</summary>
    ReadOnly = 0,

    /// <summary>The tool changes something that can be undone or is bounded.</summary>
    Actuating = 1,

    /// <summary>The tool destroys or irreversibly changes something.</summary>
    Destructive = 2,
}
