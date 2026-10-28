namespace Autonogrammer.Harness.AgentLoop.Plugins;

/// <summary>
/// How omp is told to load a plugin. omp's <c>--plugin-dir</c> flag provides skills and agents but runs no extension code,
/// and using it together with <c>-e</c> on one package registers its commands twice, so Autonogrammer loads every plugin with
/// <c>-e</c> only.
/// </summary>
public enum OmpPluginLoading
{
    /// <summary>The plugin is loaded with <c>omp acp -e &lt;path&gt;</c>.</summary>
    Extension,
}
