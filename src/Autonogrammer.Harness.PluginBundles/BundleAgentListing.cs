namespace Autonogrammer.Harness.PluginBundles;

/// <summary>An agent definition listed with a plugin-qualified name. Listing does not start the agent.</summary>
/// <param name="QualifiedName">The name plugin:agent.</param>
/// <param name="RelativePath">The path inside the bundle.</param>
public sealed record BundleAgentListing(string QualifiedName, string RelativePath);
