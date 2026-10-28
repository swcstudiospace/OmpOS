namespace Autonogrammer.UI.Components.Settings;

/// <summary>One heading and one paragraph of an <see cref="InfoSettingsPage"/>.</summary>
/// <param name="Heading">The short heading.</param>
/// <param name="Text">The paragraph under it.</param>
public sealed record InfoSettingsFact(string Heading, string Text);
