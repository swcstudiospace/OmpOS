namespace Autonogrammer.Harness.Acp;

/// <summary>One value an <see cref="AcpConfigOption"/> can take.</summary>
/// <param name="Value">The value identifier. It is sent back verbatim in <c>session/set_config_option</c>.</param>
/// <param name="Name">The display name; the value identifier when the agent sent no name.</param>
/// <param name="Description">The description the agent gave the value, or <see langword="null"/>.</param>
public sealed record AcpConfigOptionValue(string Value, string Name, string? Description);
