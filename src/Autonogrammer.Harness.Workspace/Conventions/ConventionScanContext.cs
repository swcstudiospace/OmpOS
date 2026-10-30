namespace Autonogrammer.Workspace.Conventions;

/// <summary>What the scanner of one base directory (a workspace root or the user's home) needs: where to read, where to put results, and how to label them.</summary>
/// <param name="Source">The file tree to read.</param>
/// <param name="Collector">The collector that receives items and problems.</param>
/// <param name="Options">The bounds of the run.</param>
/// <param name="Scope">Project or user.</param>
/// <param name="BasePath">The workspace root or the user's home, in the source's path form.</param>
/// <param name="OriginPrefix">The text put before a relative path to show it: empty for a project, <c>~/</c> for the user's home.</param>
internal sealed record ConventionScanContext(
    IConventionFileSource Source,
    ConventionCollector Collector,
    ConventionDiscoveryOptions Options,
    ConventionScope Scope,
    string BasePath,
    string OriginPrefix);
