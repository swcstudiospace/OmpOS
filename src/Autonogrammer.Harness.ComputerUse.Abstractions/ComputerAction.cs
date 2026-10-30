namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Host-built action shapes live beside this marker: <see cref="ComputerActionRequest"/>, <see cref="ComputerActionTicket"/>
/// and <see cref="ComputerGateDecision"/>. The ticket is single-use, bound to the action hash, and valid for
/// <see cref="ComputerActionTicket.Lifetime"/>.
/// </summary>
public static class ComputerAction
{
}
