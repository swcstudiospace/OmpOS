namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Supplies the per-session environment variables of a worker at provisioning. It is the delegate the Railway client calls
/// (<c>RailwayCloudEnvironmentOptions.SecretVariablesProvider</c>): the values go into the worker's own Railway service and nowhere else, never into
/// shared services, never onto the volume, never into a log (ADR-0020 D7).
/// </summary>
internal interface ICloudWorkerVariableSource
{
    /// <summary>Builds the variables for the worker of a session.</summary>
    /// <param name="sessionId">The session the worker is created for.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The variables by name, secrets included; the caller must not log them. Empty for a session that is not a cloud agent's.</returns>
    /// <exception cref="CloudProvisioningException">A secret no longer exists, a repository or environment file is invalid, or the git host failed.</exception>
    ValueTask<IReadOnlyDictionary<string, string>> ResolveAsync(string sessionId, CancellationToken cancellationToken);
}
