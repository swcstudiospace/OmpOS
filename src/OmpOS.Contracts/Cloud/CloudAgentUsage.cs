namespace OmpOS.Contracts.Cloud;

/// <summary>What one owner uses of the caps.</summary>
/// <param name="Agents">How many agents the owner has.</param>
/// <param name="Active">How many of them are provisioning, starting or running.</param>
/// <param name="ComputeSeconds">The finished awake seconds of all the owner's agents.</param>
public sealed record CloudAgentUsage(int Agents, int Active, double ComputeSeconds);
