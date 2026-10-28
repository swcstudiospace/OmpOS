using System.Globalization;

namespace OmpOS.Harness.Mcp.Config;

/// <summary>
/// One MCP server as configured, after variable expansion. The expanded environment values, header values and URL are secrets:
/// <see cref="ToString"/> prints the name and counts only, and nothing that displays a spec shows more than the names and the host.
/// </summary>
public sealed class McpServerSpec
{
    /// <summary>Gets the server name, unique among the effective servers.</summary>
    public required string Name { get; init; }

    /// <summary>Gets how omp reaches the server.</summary>
    public required McpTransportKind Transport { get; init; }

    /// <summary>Gets where the file that declares the server lives.</summary>
    public required McpScope Scope { get; init; }

    /// <summary>Gets the dialect of the file that declares the server.</summary>
    public required McpConfigFormat Format { get; init; }

    /// <summary>Gets the file to show: relative to the workspace root, or <c>~/</c> relative.</summary>
    public required string Origin { get; init; }

    /// <summary>Gets the position of the declaring file in precedence order; lower wins.</summary>
    public required int Priority { get; init; }

    /// <summary>Gets the executable of a stdio server, expanded; <see langword="null"/> for a remote server.</summary>
    public string? Command { get; init; }

    /// <summary>Gets the arguments of a stdio server, expanded, in order.</summary>
    public IReadOnlyList<string> Args { get; init; } = [];

    /// <summary>Gets the environment variables added to a stdio server, expanded. The values are secrets.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the endpoint of a remote server, expanded; <see langword="null"/> for a stdio server or when the URL is not usable yet.</summary>
    public Uri? Url { get; init; }

    /// <summary>Gets the request headers of a remote server, expanded. The values are secrets.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets whether the declaring file leaves the server switched on. A file may say <c>"disabled": true</c>.</summary>
    public bool EnabledInFile { get; init; } = true;

    /// <summary>Gets whether the entry carries OAuth settings (<c>auth</c> or <c>oauth</c>). They are not passed to omp.</summary>
    public bool HasOAuthSettings { get; init; }

    /// <summary>Gets whether any field reads the environment. A project server that does could send a secret to its URL.</summary>
    public bool UsesEnvironmentVariables { get; init; }

    /// <summary>Gets the variable references that could not be expanded, without <c>${</c> and <c>}</c>. Names only.</summary>
    public IReadOnlyList<string> UnresolvedVariables { get; init; } = [];

    /// <summary>Gets the command line as written, before expansion, for the consent prompt; or the host of a remote server. No secret is expanded into it.</summary>
    public string CommandPreview { get; init; } = string.Empty;

    /// <summary>Gets the consent identity: a hash of the declared command, arguments, environment, URL and headers as written. See <see cref="McpConsentIdentity"/>.</summary>
    public required string ConsentIdentity { get; init; }

    /// <summary>
    /// Gets whether the user must approve the server before it is offered to a session. A project server that starts a process
    /// needs consent, and so does a project server that reads the environment: its URL could otherwise collect a secret.
    /// </summary>
    public bool RequiresConsent => Scope == McpScope.Project && (Transport == McpTransportKind.Stdio || UsesEnvironmentVariables);

    /// <summary>Gets whether every variable is expanded and the entry has what its transport needs, so it can be passed to omp.</summary>
    public bool IsComplete =>
        UnresolvedVariables.Count == 0
        && (Transport == McpTransportKind.Stdio ? !string.IsNullOrWhiteSpace(Command) : Url is not null);

    /// <summary>Gets the scheme, host and port of a remote server, or an empty string. The path and query are not shown: they may hold a secret.</summary>
    public string HostDisplay => Url is null
        ? string.Empty
        : Url.GetLeftPart(UriPartial.Authority);

    /// <summary>Gets the environment variable names, in ordinal order. Names only.</summary>
    public IReadOnlyList<string> EnvNames => [.. Env.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Gets the header names, in ordinal order. Names only.</summary>
    public IReadOnlyList<string> HeaderNames => [.. Headers.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Prints the name, transport and counts only: arguments, URL, environment values and header values may be secrets.</summary>
    /// <returns>A one-line description without any secret.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"McpServerSpec {{ Name = {Name}, Transport = {Transport}, Scope = {Scope}, ArgumentCount = {Args.Count}, EnvironmentVariableCount = {Env.Count}, HeaderCount = {Headers.Count} }}");
}
