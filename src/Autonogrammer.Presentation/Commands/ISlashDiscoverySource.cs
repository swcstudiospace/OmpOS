namespace Autonogrammer.Presentation.Commands;

/// <summary>Reads the custom commands, skills and subagents of a session's workspace. The desktop head implements it over <c>ConventionCatalog</c>; a remote or cloud head implements it over its own file tree.</summary>
public interface ISlashDiscoverySource
{
    /// <summary>Reads the conventions now. Each call reads again, so calling it is the manual refresh.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What was found. A file that cannot be used is a problem sentence, not an exception.</returns>
    Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken);
}
