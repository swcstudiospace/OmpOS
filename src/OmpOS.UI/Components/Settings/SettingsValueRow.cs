namespace OmpOS.UI.Components.Settings;

/// <summary>One labelled value of a read-only values page.</summary>
/// <param name="Label">What the value is.</param>
/// <param name="Value">The value as text.</param>
/// <param name="Help">A sentence that says what the value means, or null.</param>
public sealed record SettingsValueRow(string Label, string Value, string? Help = null);
