namespace OmpOS.Harness.Plugins;

/// <summary>
/// A validated plugin manifest. A manifest carries no code: its only executable parts are hook commands, and each
/// hook command runs only after consent for its exact command.
/// </summary>
/// <param name="Id">The plugin identifier. It is unique within a host.</param>
/// <param name="Version">The plugin version, a dotted numeric version such as <c>1.2.0</c>.</param>
/// <param name="ApiVersion">The plugin API version the manifest was written against.</param>
/// <param name="Capabilities">The capabilities the plugin declares.</param>
/// <param name="Permissions">The permissions the plugin requests.</param>
/// <param name="Hooks">The lifecycle hooks, in manifest order.</param>
public sealed record PluginManifest(
    string Id,
    string Version,
    int ApiVersion,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PluginHookDeclaration> Hooks);
