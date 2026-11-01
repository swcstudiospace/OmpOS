namespace Autonogrammer.Harness.PluginBundles;

/// <summary>One kind of content a bundle may hold. Hooks are listed and never run.</summary>
public enum PluginBundleComponent
{
    /// <summary>The optional <c>.claude-plugin/plugin.json</c> manifest.</summary>
    Manifest = 0,

    /// <summary>Skill folders under <c>skills</c>.</summary>
    Skills = 1,

    /// <summary>Slash command files under <c>commands</c>.</summary>
    Commands = 2,

    /// <summary>Agent definition files under <c>agents</c>.</summary>
    Agents = 3,

    /// <summary>The optional <c>.mcp.json</c> server file.</summary>
    Mcp = 4,

    /// <summary>The optional <c>hooks/hooks.json</c> file. It is listed and never executed.</summary>
    Hooks = 5,
}
