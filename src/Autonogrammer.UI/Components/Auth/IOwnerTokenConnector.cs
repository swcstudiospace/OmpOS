namespace Autonogrammer.UI.Components.Auth;

/// <summary>Checks an owner token against the API and, when the API accepts it, makes it the token of the browser session.</summary>
public interface IOwnerTokenConnector
{
    /// <summary>Tries the token with a cheap authenticated request. The token is kept in memory only, and only when the API accepted it.</summary>
    /// <param name="token">The token the operator typed. It is never logged.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>What came of the attempt.</returns>
    Task<ConnectOutcome> ConnectAsync(string token, CancellationToken cancellationToken);
}
