using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// One scripted turn: the events it streams in order, whether it holds until the client cancels it, and the fault that ends it before any event.
/// </summary>
/// <param name="Events">The events the turn streams.</param>
/// <param name="HoldUntilCancelled">Whether the turn ends only when the client cancels it, after its events.</param>
/// <param name="Fault">The message of the fault that ends the turn before any event, or null when it does not fault.</param>
internal sealed record ScriptedTurn(IReadOnlyList<HarnessEvent> Events, bool HoldUntilCancelled = false, string? Fault = null);
