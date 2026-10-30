namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// A hook's permission answer. When hooks disagree, <see cref="Deny"/> beats <see cref="Ask"/> beats <see cref="Allow"/>
/// whatever source each hook came from.
/// </summary>
public enum HookVerdict
{
    /// <summary>Let the action proceed. This does not bypass omp's own approval policy.</summary>
    Allow,

    /// <summary>Ask the user before the action runs.</summary>
    Ask,

    /// <summary>Block the action.</summary>
    Deny,
}
