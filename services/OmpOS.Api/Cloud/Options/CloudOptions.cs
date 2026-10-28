namespace OmpOS.Api.Cloud.Options;

/// <summary>
/// Binds the <c>Cloud</c> configuration section: the cloud agent control plane. Cloud is off unless <see cref="Enabled"/> is true; when it
/// is on, <see cref="CloudOptionsValidator"/> checks the whole section on start and a missing key stops the host, naming itself. Several members
/// hold secrets (the Railway token, the signing key, the GitHub App key, the PAT and the operator secrets). They are read from configuration
/// sources such as environment variables or a secret store, never from <c>appsettings.json</c>, and no code path logs them.
/// </summary>
internal sealed class CloudOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Cloud";

    /// <summary>Gets or sets whether the cloud agent endpoints and background services are active. Defaults to false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets where worker environments come from.</summary>
    public EnvironmentSettings Environment { get; set; } = new();

    /// <summary>Gets or sets the per-owner caps and the idle and compute windows.</summary>
    public LimitSettings Limits { get; set; } = new();

    /// <summary>Gets or sets how often the idle sweeper and the orphan scan run.</summary>
    public SweeperSettings Sweeper { get; set; } = new();

    /// <summary>Gets or sets how the API reaches and authenticates to workers.</summary>
    public WorkerSettings Worker { get; set; } = new();

    /// <summary>Gets or sets the git host credentials and pull request behaviour.</summary>
    public GitSettings Git { get; set; } = new();

    /// <summary>Gets or sets the operator-defined secrets every owner may reference.</summary>
    public SecretSettings Secrets { get; set; } = new();

    /// <summary>Gets or sets the named environment definitions an agent may select with <c>environmentId</c>.</summary>
    public Dictionary<string, EnvironmentDefinition> Environments { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets whether the configured provider can give a worker per-session environment variables, which is how secrets reach it.</summary>
    public bool SupportsSessionVariables =>
        !string.Equals(Environment.Provider, EnvironmentSettings.StaticPoolProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>Cloud:Environment</c> section: which environment client provisions workers.</summary>
    internal sealed class EnvironmentSettings
    {
        /// <summary>The provider name for one Railway service plus one volume per agent.</summary>
        public const string RailwayProvider = "Railway";

        /// <summary>The provider name for a fixed pool of always-warm workers.</summary>
        public const string StaticPoolProvider = "StaticPool";

        /// <summary>The provider name for a host that registers its own <c>ICloudEnvironmentClient</c> (tests, other tiers).</summary>
        public const string ExternalProvider = "External";

        /// <summary>Gets or sets the provider: <c>Railway</c>, <c>StaticPool</c> or <c>External</c>. Required when cloud is enabled.</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>Gets or sets the Railway settings. Read only for <c>Railway</c>.</summary>
        public RailwaySettings Railway { get; set; } = new();

        /// <summary>Gets or sets the static pool settings. Read only for <c>StaticPool</c>.</summary>
        public StaticPoolSettings StaticPool { get; set; } = new();

        /// <summary>Gets or sets where the volume is mounted in the worker. Must match the worker image. Defaults to <c>/data</c>.</summary>
        public string VolumeMountPath { get; set; } = "/data";
    }

    /// <summary>The <c>Cloud:Environment:Railway</c> section.</summary>
    internal sealed class RailwaySettings
    {
        /// <summary>Gets or sets the Railway API endpoint. Defaults to the client's own default.</summary>
        public string? Endpoint { get; set; }

        /// <summary>Gets or sets the workspace-scoped Railway API token. A secret.</summary>
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>Gets or sets the Railway project the worker services are created in.</summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>Gets or sets the Railway environment the services run in.</summary>
        public string EnvironmentId { get; set; } = string.Empty;

        /// <summary>Gets or sets the worker image, for example <c>ghcr.io/swcstudiospace/ompos-agent-worker:1.2.3</c>.</summary>
        public string Image { get; set; } = string.Empty;

        /// <summary>Gets or sets the public key PEMs the worker verifies tokens with. They go to the worker as <c>Worker__Auth__PublicKeysPem__N</c>.</summary>
        public List<string> WorkerPublicKeysPem { get; set; } = [];
    }

    /// <summary>The <c>Cloud:Environment:StaticPool</c> section.</summary>
    internal sealed class StaticPoolSettings
    {
        /// <summary>Gets or sets the always-warm workers.</summary>
        public List<StaticPoolWorkerSettings> Workers { get; set; } = [];
    }

    /// <summary>One worker of the static pool.</summary>
    internal sealed class StaticPoolWorkerSettings
    {
        /// <summary>Gets or sets the worker identifier.</summary>
        public string WorkerId { get; set; } = string.Empty;

        /// <summary>Gets or sets the worker's base URI.</summary>
        public string BaseUri { get; set; } = string.Empty;

        /// <summary>Gets or sets where the worker's volume is mounted. Defaults to <c>/data</c>.</summary>
        public string VolumeMountPath { get; set; } = "/data";
    }

    /// <summary>The <c>Cloud:Limits</c> section.</summary>
    internal sealed class LimitSettings
    {
        /// <summary>Gets or sets how many agents one owner may have. Defaults to 5.</summary>
        public int MaxAgentsPerOwner { get; set; } = 5;

        /// <summary>Gets or sets how many agents of one owner may be provisioning, starting or running at once. Defaults to 2.</summary>
        public int MaxConcurrentRunningPerOwner { get; set; } = 2;

        /// <summary>Gets or sets the idle window in minutes when a request names none. Defaults to 15 (research decision K5).</summary>
        public int DefaultIdleMinutes { get; set; } = 15;

        /// <summary>Gets or sets the longest idle window a request may ask for, in minutes. Defaults to 1440.</summary>
        public int MaxIdleMinutes { get; set; } = 1440;

        /// <summary>Gets or sets the compute seconds one agent may use before it is stopped. 0 means no limit, which is the default.</summary>
        public long MaxComputeSecondsPerAgent { get; set; }

        /// <summary>Gets or sets how many secrets one owner may store. Defaults to 64.</summary>
        public int MaxSecretsPerOwner { get; set; } = 64;
    }

    /// <summary>The <c>Cloud:Sweeper</c> section.</summary>
    internal sealed class SweeperSettings
    {
        /// <summary>Gets or sets how often idle agents are put to idle and stuck agents are repaired. Defaults to one minute.</summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>Gets or sets how often records and workers are compared for orphans. Defaults to ten minutes.</summary>
        public TimeSpan OrphanScanInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>Gets or sets how long an agent may stay in provisioning or starting before it is marked failed. Defaults to twenty minutes.</summary>
        public TimeSpan ProvisioningTimeout { get; set; } = TimeSpan.FromMinutes(20);

        /// <summary>Gets or sets whether workers without a record are removed. Off by default: they are only reported.</summary>
        public bool OrphanCleanup { get; set; }
    }

    /// <summary>The <c>Cloud:Worker</c> section.</summary>
    internal sealed class WorkerSettings
    {
        /// <summary>Gets the longest lifetime the worker accepts for a token.</summary>
        public static readonly TimeSpan MaximumTokenLifetime = TimeSpan.FromMinutes(15);

        /// <summary>Gets or sets the ES256 signing key as a PKCS#8 PEM (EC P-256). A secret. Required when cloud is enabled.</summary>
        public string SigningKeyPem { get; set; } = string.Empty;

        /// <summary>Gets or sets the <c>iss</c> claim; the worker's <c>Worker:Auth:Issuer</c> must match. Defaults to <c>ompos-api</c>.</summary>
        public string Issuer { get; set; } = "ompos-api";

        /// <summary>Gets or sets how long a worker token lives. At most fifteen minutes; defaults to five.</summary>
        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Gets or sets the directory on the worker under which every session has its own directory. Must match <c>Worker:WorkspaceRoot</c>. Defaults to <c>/data/work</c>.</summary>
        public string WorkspaceRoot { get; set; } = "/data/work";

        /// <summary>Gets or sets the permission kinds that reach the approval flow. Every other kind is denied. Defaults to the hosted policy: edit, delete, move, execute, read, search.</summary>
        public List<string> ApprovalKinds { get; set; } = ["edit", "delete", "move", "execute", "read", "search"];
    }

    /// <summary>The <c>Cloud:Git</c> section.</summary>
    internal sealed class GitSettings
    {
        /// <summary>Gets or sets the GitHub REST API base. Defaults to <c>https://api.github.com</c>.</summary>
        public string ApiBase { get; set; } = "https://api.github.com";

        /// <summary>Gets or sets the repository hosts a request may name. Defaults to <c>github.com</c>.</summary>
        public List<string> AllowedHosts { get; set; } = ["github.com"];

        /// <summary>Gets or sets the GitHub App settings (preferred).</summary>
        public GitAppSettings App { get; set; } = new();

        /// <summary>Gets or sets a fine-grained personal access token used when no App is configured. A secret; long-lived, so weaker than the App.</summary>
        public string Pat { get; set; } = string.Empty;

        /// <summary>Gets or sets the pull request behaviour.</summary>
        public PullRequestSettings PullRequests { get; set; } = new();
    }

    /// <summary>The <c>Cloud:Git:App</c> section.</summary>
    internal sealed class GitAppSettings
    {
        /// <summary>Gets or sets the GitHub App identifier.</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>Gets or sets the App's RSA private key as a PEM. A secret.</summary>
        public string PrivateKeyPem { get; set; } = string.Empty;

        /// <summary>Gets or sets the installation identifier. When empty the installation of each repository is looked up.</summary>
        public string InstallationId { get; set; } = string.Empty;
    }

    /// <summary>The <c>Cloud:Git:PullRequests</c> section.</summary>
    internal sealed class PullRequestSettings
    {
        /// <summary>Gets or sets whether a pull request is never opened as a draft, whatever the request says. Defaults to false.</summary>
        public bool NeverDraft { get; set; }
    }

    /// <summary>The <c>Cloud:Secrets</c> section.</summary>
    internal sealed class SecretSettings
    {
        /// <summary>Gets or sets operator-defined secrets by name, available to every owner. Values come from the host's secret store, not from a committed file.</summary>
        public Dictionary<string, string> Static { get; set; } = new(StringComparer.Ordinal);
    }

    /// <summary>A named environment definition: what a worker runs after checkout and at each wake.</summary>
    internal sealed class EnvironmentDefinition
    {
        /// <summary>Gets or sets the install command.</summary>
        public string? Install { get; set; }

        /// <summary>Gets or sets the start command.</summary>
        public string? Start { get; set; }

        /// <summary>Gets or sets the terminals to open.</summary>
        public List<EnvironmentTerminalDefinition> Terminals { get; set; } = [];
    }

    /// <summary>One terminal of a named environment definition.</summary>
    internal sealed class EnvironmentTerminalDefinition
    {
        /// <summary>Gets or sets the terminal name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the command the terminal runs.</summary>
        public string Command { get; set; } = string.Empty;
    }
}
