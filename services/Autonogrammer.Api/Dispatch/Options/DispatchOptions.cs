namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Binds the <c>Dispatch</c> section. The feature is off by default. Key material is never included in <see cref="ToString"/>.
/// </summary>
internal sealed class DispatchOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Dispatch";

    /// <summary>Gets or sets whether dispatch routes answer. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets pairing freshness.</summary>
    public PairingSettings Pairing { get; set; } = new();

    /// <summary>Gets or sets device-credential signing.</summary>
    public CredentialSettings Credentials { get; set; } = new();

    /// <summary>Gets or sets command-frame signing. Distinct from the credential key.</summary>
    public CommandSigningSettings CommandSigning { get; set; } = new();

    /// <summary>Gets or sets the largest command payload, in bytes. Default 65536.</summary>
    public int MaxPayloadBytes { get; set; } = 65_536;

    /// <summary>Gets or sets how long a queued command stays deliverable. Default 5 minutes.</summary>
    public TimeSpan QueueTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the clock skew applied to credential lifetime. Default 30 seconds.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets queue and device caps.</summary>
    public LimitSettings Limits { get; set; } = new();

    /// <summary>Gets or sets per-operation run limits.</summary>
    public RunLimitSettings RunLimits { get; set; } = new();

    /// <summary>Gets or sets how long a local approval may wait. Default 5 minutes.</summary>
    public TimeSpan CommandApprovalTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the background sweeper. Tests turn it off and call the sweep directly.</summary>
    public SweeperSettings Sweeper { get; set; } = new();

    /// <summary>Gets or sets whether this process is the only dispatch replica.</summary>
    public DispatchReplicaMode ReplicaMode { get; set; } = DispatchReplicaMode.Single;

    /// <summary>Gets or sets the hub origin returned at pairing. Empty uses the request host.</summary>
    public string HubOrigin { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => "DispatchOptions (keys not shown)";

    /// <summary>Pairing freshness.</summary>
    internal sealed class PairingSettings
    {
        /// <summary>Gets or sets the newest acceptable full sign-in age. Default 10 minutes.</summary>
        public TimeSpan MaxSignInAge { get; set; } = TimeSpan.FromMinutes(10);
    }

    /// <summary>Device credential signing. The PEM is never written by <see cref="ToString"/>.</summary>
    internal sealed class CredentialSettings
    {
        /// <summary>Gets or sets the <c>iss</c> claim.</summary>
        public string Issuer { get; set; } = "autonogrammer-dispatch";

        /// <summary>Gets or sets the credential lifetime. Default 30 days.</summary>
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(30);

        /// <summary>Gets or sets the PKCS#8 PEM private key. Never logged.</summary>
        public string SigningKeyPem { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string ToString() => "Dispatch:Credentials (key not shown)";
    }

    /// <summary>Command signing key. Distinct from <see cref="CredentialSettings.SigningKeyPem"/>.</summary>
    internal sealed class CommandSigningSettings
    {
        /// <summary>Gets or sets the PKCS#8 PEM private key. Never logged.</summary>
        public string KeyPem { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string ToString() => "Dispatch:CommandSigning (key not shown)";
    }

    /// <summary>Caps handed to <c>DispatchStoreLimits</c>.</summary>
    internal sealed class LimitSettings
    {
        /// <summary>Gets or sets queued commands per device. Default 100.</summary>
        public int MaxQueuedPerDevice { get; set; } = 100;

        /// <summary>Gets or sets devices per account. Default 20.</summary>
        public int DevicesPerAccount { get; set; } = 20;

        /// <summary>Gets or sets feed retention. Default 30 days.</summary>
        public TimeSpan FeedRetention { get; set; } = TimeSpan.FromDays(30);

        /// <summary>Gets or sets terminal body retention. Default 7 days.</summary>
        public TimeSpan BodyRetention { get; set; } = TimeSpan.FromDays(7);
    }

    /// <summary>Per-operation run limits.</summary>
    internal sealed class RunLimitSettings
    {
        /// <summary>Gets or sets the task run limit. Default 4 hours.</summary>
        public TimeSpan Task { get; set; } = TimeSpan.FromHours(4);

        /// <summary>Gets or sets the session and cancel run limit. Default 60 seconds.</summary>
        public TimeSpan Session { get; set; } = TimeSpan.FromSeconds(60);
    }

    /// <summary>Background sweeper settings.</summary>
    internal sealed class SweeperSettings
    {
        /// <summary>Gets or sets whether the background sweeper runs. Default true.</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>How many dispatch processes share the store.</summary>
    internal enum DispatchReplicaMode
    {
        /// <summary>This process is the only replica.</summary>
        Single = 0,

        /// <summary>More than one replica. Requires Dragonfly coordination and the Postgres store.</summary>
        Multi = 1,
    }
}
