namespace OmpOS.Presentation.Settings;

/// <summary>One plugin the Plugins page lists.</summary>
/// <param name="Id">The plugin identifier.</param>
/// <param name="DisplayName">The name shown to the user.</param>
/// <param name="Role">The plugin's role in a sentence or a short phrase.</param>
/// <param name="Version">The plugin version, or null when it is not known.</param>
/// <param name="Availability">Either <see cref="FoundAvailability"/> or <see cref="MissingAvailability"/>; any other text counts as missing.</param>
/// <param name="Detail">A line of detail such as where the plugin was found or what is missing, or null.</param>
public sealed record PluginRow(string Id, string DisplayName, string Role, string? Version, string Availability, string? Detail)
{
    /// <summary>The availability of a plugin that was found.</summary>
    public const string FoundAvailability = "Found";

    /// <summary>The availability of a plugin that is missing.</summary>
    public const string MissingAvailability = "Missing";

    /// <summary>Gets a value indicating whether the plugin was found.</summary>
    public bool IsFound => string.Equals(Availability, FoundAvailability, StringComparison.Ordinal);
}
