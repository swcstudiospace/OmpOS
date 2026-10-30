using System.Globalization;
using System.Text;
using Autonogrammer.Harness.AgentLoop.Substrate;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// Where the agent loop finds its plugins, keeps its state and reaches the Substrate, resolved once from the process environment.
/// It holds locations and the NAMES of the variables a secret comes from, never the secret itself: the Substrate token is read on
/// demand by <see cref="ReadSubstrateToken"/> and handed straight to the caller, so no field, <see cref="ToString"/> or log of this
/// object can carry it.
/// </summary>
internal sealed class AgentLoopEnvironment
{
    /// <summary>The variable that names the plugins root. It overrides the search for the repository's <c>plugins</c> directory.</summary>
    internal const string PluginsRootVariable = "AUTONOGRAMMER_PLUGINS_ROOT";

    /// <summary>The variable that names the root of all session state.</summary>
    internal const string StateDirectoryVariable = "AUTONOGRAMMER_STATE_DIR";

    /// <summary>The variable that holds the Substrate address. Unset means the Substrate is not used.</summary>
    internal const string SubstrateUrlVariable = "AUTONOGRAMMER_SUBSTRATE_URL";

    /// <summary>The variable that names the variable the Substrate token is read from.</summary>
    internal const string SubstrateTokenVariableNameVariable = "AUTONOGRAMMER_SUBSTRATE_TOKEN_ENV";

    /// <summary>The variable the Substrate token is read from when <see cref="SubstrateTokenVariableNameVariable"/> names none.</summary>
    internal const string DefaultSubstrateTokenVariable = "SUBSTRATE_TOKEN_OMP";

    /// <summary>The variable that names a file whose first line is the Substrate token.</summary>
    internal const string SubstrateTokenFileVariable = "SUBSTRATE_TOKEN_FILE";

    /// <summary>The variable that names the directory the Substrate extensions read tokens from.</summary>
    internal const string SubstrateTokensDirectoryVariable = "SUBSTRATE_TOKENS_DIR";

    /// <summary>The marker file that identifies the repository root when the plugins root is searched for.</summary>
    internal const string RepositoryMarker = ".gitmodules";

    private const string XdgDataHomeVariable = "XDG_DATA_HOME";
    private const string StateDirectoryName = "Autonogrammer";
    private const int MaxTokenFileBytes = 4096;

    private readonly Func<string, string?> _getVariable;

    private AgentLoopEnvironment(
        Func<string, string?> getVariable,
        string pluginsRoot,
        string stateRoot,
        string workspaceRoot,
        SubstrateEndpoint? substrate,
        string? substrateProblem,
        string substrateTokenVariable,
        string? substrateTokenFile,
        string? substrateTokensDirectory)
    {
        _getVariable = getVariable;
        PluginsRoot = pluginsRoot;
        StateRoot = stateRoot;
        WorkspaceRoot = workspaceRoot;
        Substrate = substrate;
        SubstrateProblem = substrateProblem;
        SubstrateTokenVariable = substrateTokenVariable;
        SubstrateTokenFile = substrateTokenFile;
        SubstrateTokensDirectory = substrateTokensDirectory;
    }

    /// <summary>Gets the absolute plugins root. It may not exist: the plugin stages then report Unavailable with the fix.</summary>
    public string PluginsRoot { get; }

    /// <summary>Gets the absolute root of all session state.</summary>
    public string StateRoot { get; }

    /// <summary>Gets the absolute workspace root, which is the current directory.</summary>
    public string WorkspaceRoot { get; }

    /// <summary>Gets the validated Substrate endpoint, or null when none is configured or the configured address was refused.</summary>
    public SubstrateEndpoint? Substrate { get; }

    /// <summary>Gets why a configured Substrate address was refused, or null. The text never contains the address.</summary>
    public string? SubstrateProblem { get; }

    /// <summary>Gets the NAME of the environment variable the Substrate token is read from.</summary>
    public string SubstrateTokenVariable { get; }

    /// <summary>Gets the absolute path of the file the Substrate token is read from when the variable is empty, or null.</summary>
    public string? SubstrateTokenFile { get; }

    /// <summary>Gets the directory the Substrate extensions read tokens from, or null to leave their default in force.</summary>
    public string? SubstrateTokensDirectory { get; }

    /// <summary>Resolves the environment of the running process.</summary>
    /// <returns>The resolved environment.</returns>
    public static AgentLoopEnvironment Resolve() => Resolve(
        static name => Environment.GetEnvironmentVariable(name),
        AppContext.BaseDirectory,
        Environment.CurrentDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Resolves an environment from injected process facts, so the precedence rules can be checked without touching the process.</summary>
    /// <param name="getVariable">Reads an environment variable, returning null when it is unset.</param>
    /// <param name="baseDirectory">The directory of the running application. The search for the repository starts here.</param>
    /// <param name="currentDirectory">The current directory. It is the workspace root, the second search start and the base of relative paths.</param>
    /// <param name="homeDirectory">The user's home directory.</param>
    /// <returns>The resolved environment.</returns>
    /// <exception cref="ArgumentNullException">A delegate or a directory is null.</exception>
    public static AgentLoopEnvironment Resolve(Func<string, string?> getVariable, string baseDirectory, string currentDirectory, string homeDirectory)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);
        ArgumentNullException.ThrowIfNull(homeDirectory);

        var workspace = Path.GetFullPath(currentDirectory);
        var (substrate, problem) = ResolveSubstrate(getVariable);
        var tokenVariable = Clean(getVariable(SubstrateTokenVariableNameVariable));
        var tokenFile = Clean(getVariable(SubstrateTokenFileVariable));
        var tokensDirectory = Clean(getVariable(SubstrateTokensDirectoryVariable));
        return new AgentLoopEnvironment(
            getVariable,
            ResolvePluginsRoot(getVariable, baseDirectory, workspace),
            ResolveStateRoot(getVariable, workspace, homeDirectory),
            workspace,
            substrate,
            problem,
            tokenVariable is not null && IsVariableName(tokenVariable) ? tokenVariable : DefaultSubstrateTokenVariable,
            tokenFile is null ? null : Path.GetFullPath(tokenFile, workspace),
            tokensDirectory is null ? null : Path.GetFullPath(tokensDirectory, workspace));
    }

    /// <summary>
    /// Reads the Substrate token now: the variable named by <see cref="SubstrateTokenVariable"/> when it is set, otherwise the first
    /// line of <see cref="SubstrateTokenFile"/>. The value is returned to the caller and kept nowhere.
    /// </summary>
    /// <returns>The token, or null when none is configured or the file cannot be read.</returns>
    public string? ReadSubstrateToken()
    {
        var fromVariable = Clean(_getVariable(SubstrateTokenVariable));
        if (fromVariable is not null)
        {
            return fromVariable;
        }

        return SubstrateTokenFile is null ? null : ReadTokenFile(SubstrateTokenFile);
    }

    /// <summary>Describes the environment by its locations and the names of its variables. It never prints a token.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"AgentLoopEnvironment {{ PluginsRoot = {PluginsRoot}, StateRoot = {StateRoot}, WorkspaceRoot = {WorkspaceRoot}, Substrate = {(Substrate is null ? "disabled" : Substrate.ToString())}, TokenVariable = {SubstrateTokenVariable}, TokenFile = {(SubstrateTokenFile is null ? "none" : "configured")} }}");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsVariableName(string name)
    {
        if (name.Length == 0 || char.IsAsciiDigit(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolvePluginsRoot(Func<string, string?> getVariable, string baseDirectory, string workspace)
    {
        var configured = Clean(getVariable(PluginsRootVariable));
        if (configured is not null)
        {
            return Path.GetFullPath(configured, workspace);
        }

        foreach (var start in new[] { baseDirectory, workspace })
        {
            if (FindRepositoryRoot(start) is { } root)
            {
                return Path.Combine(root, "plugins");
            }
        }

        // No repository was found: the plugin stages report the missing directory instead of the window failing to open.
        return Path.Combine(workspace, "plugins");
    }

    private static string? FindRepositoryRoot(string start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private static string ResolveStateRoot(Func<string, string?> getVariable, string workspace, string homeDirectory)
    {
        var configured = Clean(getVariable(StateDirectoryVariable));
        if (configured is not null)
        {
            return Path.GetFullPath(configured, workspace);
        }

        var dataHome = Clean(getVariable(XdgDataHomeVariable));
        if (dataHome is null || !Path.IsPathRooted(dataHome))
        {
            dataHome = string.IsNullOrWhiteSpace(homeDirectory)
                ? Path.Combine(Path.GetTempPath(), "autonogrammer-data")
                : Path.Combine(homeDirectory, ".local", "share");
        }

        return Path.Combine(dataHome, StateDirectoryName);
    }

    private static (SubstrateEndpoint? Endpoint, string? Problem) ResolveSubstrate(Func<string, string?> getVariable)
    {
        var address = Clean(getVariable(SubstrateUrlVariable));
        if (address is null)
        {
            return (null, null);
        }

        try
        {
            return (SubstrateEndpoint.Parse(address), null);
        }
        catch (ArgumentException)
        {
            // The message of the exception is not shown: the address may carry a credential.
            return (null, $"{SubstrateUrlVariable} is not used: it must be an https address, or an http address on a loopback host, without user information, a query or a fragment.");
        }
    }

    private static string? ReadTokenFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[MaxTokenFileBytes];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = stream.Read(buffer, length, buffer.Length - length);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, length);
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
