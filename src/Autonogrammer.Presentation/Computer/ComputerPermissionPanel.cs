namespace Autonogrammer.Presentation.Computer;

/// <summary>Guidance for one availability or permission state. A missing permission never names a fallback mechanism.</summary>
public sealed record ComputerPermissionPanel
{
    /// <summary>Panel title.</summary>
    public required string Title { get; init; }

    /// <summary>What the permission or state allows, or why it does not.</summary>
    public required string WhatItAllows { get; init; }

    /// <summary>Deep link for Open System Settings, or null when there is nothing to open.</summary>
    public string? DeepLink { get; init; }

    /// <summary>Why the state is blocked, or null when Computer Use can arm.</summary>
    public string? WhyBlocked { get; init; }

    /// <summary>Stable message id. Never a window title.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets a value indicating whether Open System Settings has a target.</summary>
    public bool HasDeepLink => !string.IsNullOrWhiteSpace(DeepLink);
}
