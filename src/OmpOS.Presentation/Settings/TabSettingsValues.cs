namespace OmpOS.Presentation.Settings;

/// <summary>The read-only status of inline completion, as the head reports it.</summary>
/// <param name="Enabled">True when a completion provider is configured.</param>
/// <param name="EndpointHost">The provider's host, with its port when it is not the scheme default; never a full URL. Null when none is configured.</param>
/// <param name="Source">Where the configuration comes from, for example the name of an environment variable.</param>
public sealed record TabSettingsValues(bool Enabled, string? EndpointHost, string Source);
