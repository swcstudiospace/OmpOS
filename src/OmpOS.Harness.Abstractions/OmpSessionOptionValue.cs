namespace OmpOS.Harness.Abstractions;

/// <summary>One value an <see cref="OmpSessionOption"/> can take.</summary>
/// <param name="Value">The engine's identifier of the value. It is sent back verbatim to select it.</param>
/// <param name="Name">The display name the engine gave the value.</param>
/// <param name="Description">The description the engine gave the value, or <see langword="null"/>.</param>
public sealed record OmpSessionOptionValue(string Value, string Name, string? Description);
