namespace OmpOS.AgentWorker.Hooks;

/// <summary>What the hooks add to the launch of a session's <c>omp acp</c> child: the extension to load and the bridge's address and token.</summary>
/// <param name="ExtraArguments">The arguments appended after the configured ones, one argv element each (<c>-e</c> and the extension directory). Never a secret.</param>
/// <param name="Environment">The environment variables of the child: the bridge URL and its bearer token, and the startup file path. The token is secret: hand it only to the child and never log it.</param>
internal sealed record CloudHooksLaunch(IReadOnlyList<string> ExtraArguments, IReadOnlyDictionary<string, string> Environment)
{
    /// <summary>Gets the launch of a session without hooks: nothing is added.</summary>
    public static CloudHooksLaunch None { get; } = new([], new Dictionary<string, string>(StringComparer.Ordinal));
}
