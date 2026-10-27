namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// How a hook's standard output parsed.
/// </summary>
public enum HookOutputKind
{
    /// <summary>Standard output was empty or whitespace: the hook said nothing.</summary>
    Empty,

    /// <summary>Standard output is a JSON object that matches the event's schema.</summary>
    Valid,

    /// <summary>Standard output is not JSON, or does not match the event's schema.</summary>
    Invalid,
}
