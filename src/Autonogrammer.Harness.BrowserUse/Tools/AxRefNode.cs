namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>One accessibility node used to mint a ref.</summary>
/// <param name="BackendDomNodeId">The backend node id.</param>
/// <param name="Role">The role.</param>
/// <param name="Name">The accessible name.</param>
/// <param name="X">The box x.</param>
/// <param name="Y">The box y.</param>
public sealed record AxRefNode(int BackendDomNodeId, string Role, string Name, double X, double Y);
