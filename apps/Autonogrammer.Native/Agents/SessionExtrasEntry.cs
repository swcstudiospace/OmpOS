namespace Autonogrammer.Native.Agents;

/// <summary>One entry of <see cref="SessionExtrasDocument"/>.</summary>
internal sealed class SessionExtrasEntry
{
    /// <summary>Gets or sets the ssh environment profile the session runs on, or null for a local session.</summary>
    public string? EnvironmentId { get; set; }

    /// <summary>Gets or sets the other roots of a multi-root workspace.</summary>
    public List<string>? AdditionalDirectories { get; set; }
}
