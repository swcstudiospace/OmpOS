namespace Autonogrammer.Harness.PluginBundles;

/// <summary>A disabled skill the skills filter cannot name exactly, so its bundle skill root is not contributed.</summary>
/// <param name="BundleId">The bundle id.</param>
/// <param name="SkillId">The skill folder name.</param>
/// <param name="Reason">A fixed sentence. The skill is not included.</param>
public sealed record BundleSkillExclusionReport(string BundleId, string SkillId, string Reason);
