using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Web.Demo;

/// <summary>One scripted event of a Demo turn.</summary>
/// <param name="Payload">The payload the step appends to the session log.</param>
/// <param name="Typed">True for streamed text: the step waits one typing delay. Other steps wait one step delay.</param>
/// <param name="Effect">What else the step changes (a pending approval, a changed file), or null.</param>
internal sealed record DemoStep(StreamEventPayload Payload, bool Typed, Action<DemoSession>? Effect = null);
