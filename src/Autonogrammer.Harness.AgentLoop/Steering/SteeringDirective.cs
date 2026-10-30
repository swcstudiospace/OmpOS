using System.Globalization;

namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>
/// A message for a running omp session: how to deliver it and what it says. The identifier and the time are chosen by the
/// channel. The text is operator input, so <see cref="ToString"/> never prints it.
/// </summary>
public sealed record SteeringDirective
{
    /// <summary>Gets how the message is delivered.</summary>
    public required SteeringDirectiveMode Mode { get; init; }

    /// <summary>Gets the message. It is 1 to 8192 UTF-8 bytes and not blank.</summary>
    public required string Text { get; init; }

    /// <summary>Returns a description that names the mode and the length of the text only.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"SteeringDirective {{ Mode = {Mode}, Length = {Text.Length} }}");
}
