namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Placeholder for the host-observed target. The model never fills this record.
/// Later units replace the placeholder fields without changing the request's meaning.
/// </summary>
public sealed record ComputerObservedTarget
{
    /// <summary>Window id from <see cref="IComputerUseBackend.ListWindowsAsync"/>, when the host has resolved one.</summary>
    public string? WindowId { get; init; }

    /// <summary>Application identity from the same window list.</summary>
    public string? AppIdentity { get; init; }
}

