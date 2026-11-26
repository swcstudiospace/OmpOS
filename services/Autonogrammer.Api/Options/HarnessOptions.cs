using System.ComponentModel.DataAnnotations;
using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Api.Options;

/// <summary>
/// Binds the <c>Harness</c> configuration section: which harness adapter serves prompts. <c>InMemory</c> is the development
/// echo adapter (the build-time OpenAPI generator can start the host with it, explicitly setting <c>Development</c>). <c>Rpc</c>
/// runs omp in RPC mode over stdio and is the shipped default transport (Phase 84 cutover); <c>Acp</c> runs <c>omp acp</c> over
/// stdio and stays as the supported rollback and the remote-boundary transport. Outside Development the adapter must be <c>Acp</c> or <c>Rpc</c>.
/// </summary>
internal sealed class HarnessOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Harness";

    /// <summary>Gets the in-memory development adapter name.</summary>
    public const string InMemoryAdapter = "InMemory";

    /// <summary>Gets the ACP adapter name: omp in ACP mode over stdio.</summary>
    public const string AcpAdapter = "Acp";

    /// <summary>Gets the RPC adapter name: omp in RPC mode over stdio (default transport since Phase 84; <see cref="AcpAdapter"/> stays as the supported rollback).</summary>
    public const string RpcAdapter = "Rpc";

    /// <summary>Gets or sets the adapter name: <c>InMemory</c>, <c>Acp</c> or <c>Rpc</c>.</summary>
    [Required(ErrorMessage = "Harness:Adapter is required but was not provided. Set Harness:Adapter (supported values: InMemory, Acp, Rpc).")]
    public string Adapter { get; set; } = string.Empty;

    /// <summary>Gets or sets the ACP settings. Read only when <see cref="Adapter"/> is <c>Acp</c>.</summary>
    public AcpSettings Acp { get; set; } = new();

    /// <summary>Gets or sets the RPC settings. Read only when <see cref="Adapter"/> is <c>Rpc</c>.</summary>
    public RpcSettings Rpc { get; set; } = new();

    /// <summary>Returns whether <paramref name="adapter"/> names a harness adapter this host can serve.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is supported.</returns>
    public static bool IsSupportedAdapter(string? adapter) => IsInMemoryAdapter(adapter) || IsAcpAdapter(adapter) || IsRpcAdapter(adapter);

    /// <summary>Returns whether <paramref name="adapter"/> names the in-memory development adapter.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is <c>InMemory</c>.</returns>
    public static bool IsInMemoryAdapter(string? adapter) => string.Equals(adapter, InMemoryAdapter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether <paramref name="adapter"/> names the ACP adapter.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is <c>Acp</c>.</returns>
    public static bool IsAcpAdapter(string? adapter) => string.Equals(adapter, AcpAdapter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns whether <paramref name="adapter"/> names the RPC adapter.</summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <returns>True when the adapter is <c>Rpc</c>.</returns>
    public static bool IsRpcAdapter(string? adapter) => string.Equals(adapter, RpcAdapter, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>Harness:Acp</c> section: how the ACP adapter starts omp.</summary>
    internal sealed class AcpSettings
    {
        /// <summary>Gets or sets the omp executable, started in ACP mode. Defaults to <c>omp</c> resolved from PATH.</summary>
        public string OmpPath { get; set; } = "omp";

        /// <summary>
        /// Gets or sets which of this process's environment variables the engine inherits (<c>Harness:Acp:Environment</c>). The default depends on
        /// the host environment: <c>Allowlist</c> outside Development, <c>Inherit</c> in Development.
        /// </summary>
        public AcpEnvironmentSettings Environment { get; set; } = new();

        /// <summary>
        /// Gets or sets whether <c>Inherit</c> is accepted outside the Development environment. Defaults to false. Setting it hands every secret
        /// of this process (database passwords, provider keys, token digests) to a model-driven engine that runs shell commands: it is unsafe.
        /// </summary>
        public bool AllowInheritedEnvironment { get; set; }

        /// <summary>
        /// Gets or sets the absolute directory under which each session gets its own working directory <c>&lt;WorkspaceRoot&gt;/&lt;sessionId&gt;</c>
        /// (mode 0700) for the engine, when run worktrees are not configured. Unset by default. Required outside Development when
        /// <c>Worktrees:Root</c> and <c>Worktrees:Repository</c> are not both set, so the engine never starts in the API's own directory.
        /// </summary>
        public string? WorkspaceRoot { get; set; }

        /// <summary>
        /// Gets or sets which tool permission requests of the engine reach the user-approval flow and which of them a decision may allow
        /// (<c>Harness:Acp:Approval</c>). Defaults to deny everything.
        /// </summary>
        public HarnessApprovalOptions Approval { get; set; } = new();
    }

    /// <summary>
    /// The <c>Harness:Rpc</c> section: how the RPC adapter starts omp. It mirrors the <see cref="AcpSettings"/> fields RPC can
    /// honour. There is deliberately no <c>Approval</c> section here: omp RPC has no observed permission-request surface
    /// (OQ-525), so RPC sessions stay deny-by-default and approval-configured flows stay on <c>Acp</c>.
    /// </summary>
    internal sealed class RpcSettings
    {
        /// <summary>Gets or sets the omp executable, started in RPC mode. Defaults to <c>omp</c> resolved from PATH.</summary>
        public string OmpPath { get; set; } = "omp";

        /// <summary>
        /// Gets or sets which of this process's environment variables the engine inherits (<c>Harness:Rpc:Environment</c>). The default depends on
        /// the host environment: <c>Allowlist</c> outside Development, <c>Inherit</c> in Development.
        /// </summary>
        public AcpEnvironmentSettings Environment { get; set; } = new();

        /// <summary>
        /// Gets or sets whether <c>Inherit</c> is accepted outside the Development environment. Defaults to false. Setting it hands every secret
        /// of this process (database passwords, provider keys, token digests) to a model-driven engine that runs shell commands: it is unsafe.
        /// </summary>
        public bool AllowInheritedEnvironment { get; set; }

        /// <summary>
        /// Gets or sets the absolute directory under which each session gets its own working directory <c>&lt;WorkspaceRoot&gt;/&lt;sessionId&gt;</c>
        /// (mode 0700) for the engine, when run worktrees are not configured. Unset by default. Required outside Development when
        /// <c>Worktrees:Root</c> and <c>Worktrees:Repository</c> are not both set, so the engine never starts in the API's own directory.
        /// </summary>
        public string? WorkspaceRoot { get; set; }
    }

    /// <summary>The <c>Harness:Acp:Environment</c> section: the environment the engine process inherits.</summary>
    internal sealed class AcpEnvironmentSettings
    {
        /// <summary>
        /// Gets or sets the mode, <c>Inherit</c> or <c>Allowlist</c>. Unset means <c>Allowlist</c> outside Development and <c>Inherit</c> in Development.
        /// </summary>
        public EngineEnvironmentMode? Mode { get; set; }

        /// <summary>
        /// Gets or sets the extra variables kept in <c>Allowlist</c> mode on top of the built-in set: exact names, or prefixes when an entry
        /// ends in <c>*</c>, for example <c>ANTHROPIC_API_KEY</c> or <c>OPENAI_*</c>. Names only: values are never configured here.
        /// </summary>
        public List<string> Allow { get; set; } = [];
    }
}
