namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// One edit of <c>afterFileEdit</c>: the text that was replaced and its replacement.
/// </summary>
/// <param name="OldString">The text that was searched for.</param>
/// <param name="NewString">The text that replaced it.</param>
public sealed record HookFileEdit(string OldString, string NewString);
