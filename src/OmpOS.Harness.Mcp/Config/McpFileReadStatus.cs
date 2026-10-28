namespace OmpOS.Harness.Mcp.Config;

/// <summary>The outcome of reading one configuration file.</summary>
public enum McpFileReadStatus
{
    /// <summary>The file does not exist. This is normal: most locations hold no file.</summary>
    Missing = 0,

    /// <summary>The file was read.</summary>
    Ok = 1,

    /// <summary>The file is larger than the cap and was not read.</summary>
    TooLarge = 2,

    /// <summary>The file exists and could not be read.</summary>
    Unreadable = 3,
}
