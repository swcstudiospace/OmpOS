namespace Autonogrammer.Harness.Acp;

/// <summary>Options for starting an ACP session against <c>omp acp</c>.</summary>
public sealed record AcpClientOptions
{
    /// <summary>
    /// The ACP authentication method id omp advertises in <c>initialize</c>. Probed against omp 18.12.6: <c>authMethods</c> is
    /// <c>[{id:"agent", name:"Use existing local credentials"}]</c> in every environment, and <c>authenticate {methodId:"agent"}</c> answers
    /// <c>{}</c>, after which <c>session/new</c> works. A client that starts <c>omp acp</c> sets <see cref="AuthMethodId"/> to it; without it the
    /// handshake of every session fails before a session exists.
    /// </summary>
    public const string OmpAuthMethodId = "agent";

    /// <summary>
    /// The <see cref="HandshakeTimeout"/> a client of a real <c>omp acp</c> process sets. omp 18.12.6 answers <c>initialize</c> in about 8 seconds
    /// on an idle host (measured with a fresh home directory) and slower under load, so the 10 second default of a scripted peer would fail the
    /// first session of a cold start before omp is up.
    /// </summary>
    public static readonly TimeSpan OmpHandshakeTimeout = TimeSpan.FromSeconds(45);

    /// <summary>Gets the path to the omp executable. <see langword="null"/> resolves <c>omp</c> from <c>PATH</c>.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets the absolute working directory the session runs in. Defaults to the current directory.</summary>
    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;

    /// <summary>Gets how long the <c>initialize</c> round-trip may take before the handshake fails. Defaults to 10 seconds.</summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long any non-prompt request may take. Prompts are not bounded by this value. Defaults to 60 seconds.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets the policy that answers agent permission requests. Defaults to <see cref="AcpApprovalPolicy.DenyAll"/>.</summary>
    public AcpApprovalPolicy ApprovalPolicy { get; init; } = AcpApprovalPolicy.DenyAll;

    /// <summary>
    /// Gets the gate through which hooks see the session's traffic: the prompt, permission requests, <c>fs/*</c> calls, tool call updates
    /// and the end of each turn. <see langword="null"/> (the default) is the behaviour without hooks. A gate can only tighten what the
    /// <see cref="ApprovalPolicy"/> admits, never grant more.
    /// </summary>
    public IAcpHookGate? HookGate { get; init; }

    /// <summary>
    /// Gets the ACP authentication method id sent in <c>authenticate</c> when the agent advertises
    /// <c>authMethods</c> in its <c>initialize</c> result. <see langword="null"/> (the default) means no
    /// method is configured: a handshake that requires authentication then fails instead of guessing a method.
    /// </summary>
    public string? AuthMethodId { get; init; }

    /// <summary>
    /// Gets the absolute directory the agent may read and write through <c>fs/read_text_file</c> and
    /// <c>fs/write_text_file</c>. <see langword="null"/> (the default) disables both capabilities: <c>initialize</c>
    /// advertises them as <see langword="false"/> and agent calls are answered with a JSON-RPC error.
    /// </summary>
    public string? FsRoot { get; init; }

    /// <summary>
    /// Gets the arguments appended after <c>acp</c> on the omp command line, one argv element each and in order,
    /// for example <c>--no-extensions</c>, <c>-e</c> and a path. Defaults to none. Do not put secrets here: argv is visible to other processes.
    /// </summary>
    public IReadOnlyList<string> ExtraArguments { get; init; } = [];

    /// <summary>
    /// Gets the environment variables set on the omp child. With <see cref="EnvironmentMode"/> <see cref="EngineEnvironmentMode.Inherit"/> the child
    /// still inherits the parent environment and an entry here overrides an inherited variable of the same name; with
    /// <see cref="EngineEnvironmentMode.Allowlist"/> an entry here is one of the few variables the child gets. Values may be secrets: they are never
    /// logged or placed in a message. Defaults to none.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets how the omp child obtains the rest of its environment. <see cref="EngineEnvironmentMode.Inherit"/> (the default) is the desktop
    /// behaviour: the child sees the parent's whole environment. <see cref="EngineEnvironmentMode.Allowlist"/> clears the inherited environment
    /// and keeps only the variables <see cref="EngineEnvironmentPolicy"/> allows, so a hosted API's secrets never reach the engine's tools.
    /// Only a child process this library starts is affected; a remote or WebSocket transport has its own environment.
    /// </summary>
    public EngineEnvironmentMode EnvironmentMode { get; init; } = EngineEnvironmentMode.Inherit;

    /// <summary>
    /// Gets the extra inherited variables kept in <see cref="EngineEnvironmentMode.Allowlist"/> mode: exact names, or prefixes when an entry ends in
    /// <c>*</c>. Ignored in <see cref="EngineEnvironmentMode.Inherit"/> mode. Defaults to none.
    /// </summary>
    public IReadOnlyList<string> EnvironmentAllowList { get; init; } = [];

    /// <summary>
    /// Gets the MCP servers attached to the session in <c>session/new</c> and <c>session/load</c>. Header and environment values may
    /// be secrets: they are never logged or placed in a message. Defaults to none, which is sent as an empty list.
    /// </summary>
    public IReadOnlyList<AcpMcpServer> McpServers { get; init; } = [];
}
