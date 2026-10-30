namespace Autonogrammer.Presentation.Commands;

/// <summary>What the resolver knows besides the catalogue: the commands the engine advertises, what discovery found, and the head that runs native actions.</summary>
/// <param name="EngineCommands">The commands omp advertised for the session.</param>
/// <param name="Discovery">The custom commands, skills and subagents that discovery found.</param>
/// <param name="Host">The head's action host, or <see langword="null"/> when the window runs no native action.</param>
public sealed record SlashResolutionContext(IReadOnlyList<SlashEngineCommand> EngineCommands, SlashDiscovery Discovery, ISlashActionHost? Host)
{
    /// <summary>Gets a context that knows nothing beyond the catalogue.</summary>
    public static SlashResolutionContext Empty { get; } = new([], SlashDiscovery.Empty, null);
}
