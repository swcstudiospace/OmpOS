namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// The structured answer of a prompt hook, <c>{ ok, reason? }</c> as Cursor defines it.
/// </summary>
/// <param name="Ok">True to allow the action; false to deny it.</param>
/// <param name="Reason">Why the model decided so; sent to the agent on a deny.</param>
public sealed record PromptHookAnswer(bool Ok, string? Reason);
