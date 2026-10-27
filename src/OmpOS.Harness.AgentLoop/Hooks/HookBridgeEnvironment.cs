namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The names of the environment variables the host gives omp so that the hooks extension can reach the session's
/// <see cref="HookBridgeServer"/>. They are the contract between the host and the extension: the values come from
/// <see cref="HookBridgeServer.Environment"/>.
/// </summary>
public static class HookBridgeEnvironment
{
    /// <summary>The variable that holds the bridge URL, <c>http://127.0.0.1:&lt;port&gt;/v1/hook</c>.</summary>
    public const string Url = "OMPOS_HOOKS_URL";

    /// <summary>The variable that holds the random bearer token. The token is never written to a file and never logged.</summary>
    public const string Token = "OMPOS_HOOKS_TOKEN";

    /// <summary>The variable that holds the path of the startup file. It is set only when the options name a file path.</summary>
    public const string StartupFile = "OMPOS_HOOKS_STARTUP_FILE";
}
