namespace Autonogrammer.Harness.PluginBundles;

/// <summary>One server object inside <c>.mcp.json</c>.</summary>
internal sealed class BundleMcpServerDocument
{
    /// <summary>Gets or sets the command.</summary>
    public string? Command { get; set; }

    /// <summary>Gets or sets the arguments.</summary>
    public List<string>? Args { get; set; }

    /// <summary>Gets or sets the environment.</summary>
    public Dictionary<string, string?>? Env { get; set; }

    /// <summary>Gets or sets the working directory.</summary>
    public string? Cwd { get; set; }
}
