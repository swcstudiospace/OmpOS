namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Presses or releases one key. The key name is a host token, not a typed string.</summary>
public sealed record Key : InputPrimitive
{
    /// <summary>Host key token such as <c>Escape</c> or <c>A</c>.</summary>
    public required string Name { get; init; }

    /// <summary><see langword="true"/> for down, <see langword="false"/> for up.</summary>
    public required bool Down { get; init; }
}

