namespace Autonogrammer.Api.Cloud;

/// <summary>One terminal an environment opens after its start command.</summary>
/// <param name="Name">The terminal name.</param>
/// <param name="Command">The command it runs.</param>
internal sealed record EnvironmentTerminal(string Name, string Command);
