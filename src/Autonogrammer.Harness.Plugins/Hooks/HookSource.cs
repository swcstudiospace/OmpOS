namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// One hook configuration file the locator found candidates for, with how its hooks run.
/// </summary>
/// <param name="Scope">Where the file comes from; the scope decides priority.</param>
/// <param name="Path">The absolute path of the file.</param>
/// <param name="Format">How the file is read.</param>
/// <param name="WorkingDirectory">The absolute directory every hook of this file runs in.</param>
/// <param name="ScopeRoot">
/// The directory consent identity is relative to: the workspace root for project and Claude project files, the home
/// configuration directory for user files, the file's own directory for the managed file. A hook may not run outside it.
/// </param>
/// <param name="PseudoPluginId">
/// The id quarantine and the Hooks page group the file's hooks under, because a <c>hooks.json</c> hook has no plugin, for
/// example <c>cursor-project:3fa9c1d2e0b4</c> or <c>managed</c>.
/// </param>
/// <param name="Priority">The position of the file in merge order; lower runs first.</param>
public sealed record HookSource(
    HookScope Scope,
    string Path,
    HookConfigFormat Format,
    string WorkingDirectory,
    string ScopeRoot,
    string PseudoPluginId,
    int Priority);
