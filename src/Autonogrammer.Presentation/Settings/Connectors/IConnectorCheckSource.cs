namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>Runs one explicit connection check. Nothing calls it at startup.</summary>
public interface IConnectorCheckSource
{
    /// <summary>Checks one server.</summary>
    /// <param name="serverId">The server to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The state name and the tool names the check listed.</returns>
    Task<ConnectorCheckSnapshot> CheckAsync(string serverId, CancellationToken cancellationToken);
}
