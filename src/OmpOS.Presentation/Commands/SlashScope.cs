namespace OmpOS.Presentation.Commands;

/// <summary>Where a palette row's source lives.</summary>
public enum SlashScope
{
    /// <summary>The row is built in: the OmpOS catalogue or the engine.</summary>
    BuiltIn = 0,

    /// <summary>The row comes from a file of the open workspace.</summary>
    Project = 1,

    /// <summary>The row comes from a file in the user's home directory.</summary>
    User = 2,
}
