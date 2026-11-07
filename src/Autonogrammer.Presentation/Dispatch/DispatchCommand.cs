using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// A command as the head reports it: its identifier, operation, phase, task text and, when it failed, the closed reason.
/// </summary>
/// <param name="CommandId">The command identifier.</param>
/// <param name="Operation">The operation, from the closed set.</param>
/// <param name="Phase">The phase the command is in.</param>
/// <param name="TaskText">The task text as it was sent; the page makes it plain text before it shows it.</param>
/// <param name="Reason">The closed reason the command failed, or null when it did not fail.</param>
public sealed record DispatchCommand(string CommandId, string Operation, DispatchPhase Phase, string TaskText, DispatchFailureReason? Reason);
