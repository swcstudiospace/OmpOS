namespace Autonogrammer.Cowork.Projects;

/// <summary>The file document of the last global instructions one account read from the API.</summary>
internal sealed class GlobalInstructionsDocument
{
    /// <summary>Gets or sets the schema version.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the instructions text, or null when the account has none.</summary>
    public string? Instructions { get; set; }
}
