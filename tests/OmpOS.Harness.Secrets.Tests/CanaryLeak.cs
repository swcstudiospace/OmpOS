namespace OmpOS.Harness.Secrets.Tests;

/// <summary>One canary occurrence found in a recorded sink write.</summary>
/// <param name="Channel">The channel of the sink that recorded the write.</param>
/// <param name="CanaryIndex">The registration order of the canary that was found.</param>
/// <param name="Offset">The character offset of the canary within the channel's combined output.</param>
internal sealed record CanaryLeak(SecretSinkChannel Channel, int CanaryIndex, int Offset);
