namespace Autonogrammer.Contracts.Catalog;

/// <summary>The outcome of the workspace plugin check, in plain text.</summary>
public sealed record PluginCheckResult
{
    /// <summary>Gets the check status.</summary>
    public required PluginCheckStatus Status { get; init; }

    /// <summary>Gets a plain-text summary of the result. It never contains a path or a credential.</summary>
    public required string Summary { get; init; }
}
