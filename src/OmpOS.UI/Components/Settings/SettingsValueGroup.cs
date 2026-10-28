namespace OmpOS.UI.Components.Settings;

/// <summary>A heading and the labelled values under it, on a read-only values page.</summary>
/// <param name="Heading">The heading of the group.</param>
/// <param name="Rows">The values of the group.</param>
public sealed record SettingsValueGroup(string Heading, IReadOnlyList<SettingsValueRow> Rows);
