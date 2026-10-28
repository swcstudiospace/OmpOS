namespace OmpOS.Native.Agents;

/// <summary>What the head remembers about a session beyond its registry record: the ssh environment it runs on and the other roots of its workspace.</summary>
/// <param name="EnvironmentId">The identifier of the ssh environment profile the session runs on, or null for a local session.</param>
/// <param name="AdditionalDirectories">The absolute paths of the other roots of a multi-root workspace, in order. Empty for a single-root session.</param>
internal sealed record SessionExtras(string? EnvironmentId, IReadOnlyList<string> AdditionalDirectories)
{
    /// <summary>Gets the extras of a plain local single-root session.</summary>
    public static SessionExtras Local { get; } = new(null, []);

    /// <summary>Gets a value indicating whether the session runs on an ssh host.</summary>
    public bool IsRemote => !string.IsNullOrEmpty(EnvironmentId);
}
