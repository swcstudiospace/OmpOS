namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// One hook a manifest declares.
/// </summary>
/// <param name="Trigger">The lifecycle event that runs the hook.</param>
/// <param name="Command">The exact command: argument list and working directory.</param>
/// <param name="Blocking">
/// True when the hook may deny the action (exit code 2) and when its failure stops the hooks after it.
/// </param>
public sealed record PluginHookDeclaration(HookTrigger Trigger, HookCommand Command, bool Blocking);
