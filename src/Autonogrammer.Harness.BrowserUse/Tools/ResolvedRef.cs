namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>One resolved ref.</summary>
/// <param name="BackendDomNodeId">The accessibility backend node id.</param>
/// <param name="Role">The role.</param>
/// <param name="Name">The accessible name.</param>
/// <param name="X">The box x.</param>
/// <param name="Y">The box y.</param>
public readonly record struct ResolvedRef(int BackendDomNodeId, string Role, string Name, double X, double Y);
