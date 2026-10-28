namespace OmpOS.Native.Agents;

/// <summary>The JSON shape of <c>session-extras.json</c>: one entry per session identifier. The file holds identifiers and paths only, never a secret.</summary>
internal sealed class SessionExtrasDocument
{
    /// <summary>Gets or sets the entries by session identifier.</summary>
    public Dictionary<string, SessionExtrasEntry> Sessions { get; set; } = new(StringComparer.Ordinal);
}
