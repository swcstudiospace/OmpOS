using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Validates <see cref="DataPlaneOptions"/> and the rules that join it to the coordination, hosting and resilience sections. Every message names the key and
/// never a value, so a rejected secret cannot reach a log or a startup error.
/// </summary>
internal sealed class DataPlaneOptionsValidator : IValidateOptions<DataPlaneOptions>
{
    private readonly IOptions<SessionsOptions> _sessions;
    private readonly IOptions<CoordinationOptions> _coordination;
    private readonly IOptions<HostingOptions> _hosting;
    private readonly IOptions<PostgresResilienceOptions> _postgres;
    private readonly IOptions<DragonflyResilienceOptions> _dragonfly;
    private readonly IOptions<LedgerResilienceOptions> _ledger;

    /// <summary>Initializes a new instance of the <see cref="DataPlaneOptionsValidator"/> class.</summary>
    /// <param name="sessions">The session-store options: the tenant-hash pepper is required when the store is PostgreSQL.</param>
    /// <param name="coordination">The coordination options: the tenant-hash pepper is required when the store is Dragonfly.</param>
    /// <param name="hosting">The hosting options: the PostgreSQL budget must stay below the request timeout.</param>
    /// <param name="postgres">The PostgreSQL resilience options.</param>
    /// <param name="dragonfly">The Dragonfly resilience options.</param>
    /// <param name="ledger">The ledger resilience options.</param>
    public DataPlaneOptionsValidator(
        IOptions<SessionsOptions> sessions,
        IOptions<CoordinationOptions> coordination,
        IOptions<HostingOptions> hosting,
        IOptions<PostgresResilienceOptions> postgres,
        IOptions<DragonflyResilienceOptions> dragonfly,
        IOptions<LedgerResilienceOptions> ledger)
    {
        _sessions = sessions;
        _coordination = coordination;
        _hosting = hosting;
        _postgres = postgres;
        _dragonfly = dragonfly;
        _ledger = ledger;
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DataPlaneOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (CoordinationOptions.IsDragonflyStore(_coordination.Value.Store) && string.IsNullOrWhiteSpace(options.TenantHashPepper))
        {
            failures.Add($"{DataPlaneOptions.TenantHashPepperKey} is required when Coordination:Store is Dragonfly.");
        }

        if (SessionsOptions.IsPostgresStore(_sessions.Value.Store) && string.IsNullOrWhiteSpace(options.TenantHashPepper))
        {
            failures.Add($"{DataPlaneOptions.TenantHashPepperKey} is required when Sessions:Store is Postgres.");
        }

        var wait = options.Postgres.SchemaWaitSeconds;
        if (wait is < DataPlanePostgresOptions.MinSchemaWaitSeconds or > DataPlanePostgresOptions.MaxSchemaWaitSeconds)
        {
            failures.Add($"{DataPlanePostgresOptions.SchemaWaitSecondsKey} must be between {DataPlanePostgresOptions.MinSchemaWaitSeconds} and {DataPlanePostgresOptions.MaxSchemaWaitSeconds}.");
        }

        AddRetentionFailures(options.Retention, failures);
        AddResilienceFailures(failures);
        AddLedgerFailures(options.Ledger, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddRetentionFailures(DataPlaneRetentionOptions retention, List<string> failures)
    {
        if (retention.DispatchDays < 1)
        {
            failures.Add("DataPlane:Retention:DispatchDays must be at least 1.");
        }

        if (retention.AuditFloorDays < DataPlaneRetentionOptions.AuditFloor)
        {
            failures.Add($"DataPlane:Retention:AuditFloorDays must not be below {DataPlaneRetentionOptions.AuditFloor}.");
        }

        if (retention.AuditDays < Math.Max(retention.AuditFloorDays, DataPlaneRetentionOptions.AuditFloor))
        {
            failures.Add($"DataPlane:Retention:AuditDays must not be below DataPlane:Retention:AuditFloorDays ({DataPlaneRetentionOptions.AuditFloor} days at least).");
        }
    }

    private static void AddLedgerFailures(DataPlaneLedgerOptions ledger, List<string> failures)
    {
        if (!ledger.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ledger.Endpoint))
        {
            failures.Add("DataPlane:Ledger:Endpoint is required when DataPlane:Ledger:Enabled is true.");
        }
        else if (!IsValidEndpoint(ledger.Endpoint))
        {
            failures.Add("DataPlane:Ledger:Endpoint must be an absolute https address, or http on a loopback host, without user information, query or fragment.");
        }

        if (string.IsNullOrWhiteSpace(ledger.Token))
        {
            failures.Add("DataPlane:Ledger:Token is required when DataPlane:Ledger:Enabled is true.");
        }
    }

    private static bool IsValidEndpoint(string endpoint)
    {
        try
        {
            _ = SubstrateEndpoint.Parse(endpoint);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void AddResilienceFailures(List<string> failures)
    {
        var postgres = _postgres.Value;
        var section = PostgresResilienceOptions.SectionName;
        Positive(failures, section, nameof(postgres.ConnectSeconds), postgres.ConnectSeconds);
        Positive(failures, section, nameof(postgres.InteractiveCommandSeconds), postgres.InteractiveCommandSeconds);
        Positive(failures, section, nameof(postgres.BackgroundCommandSeconds), postgres.BackgroundCommandSeconds);
        Positive(failures, section, nameof(postgres.BudgetSeconds), postgres.BudgetSeconds);
        Positive(failures, section, nameof(postgres.PoolInteractive), postgres.PoolInteractive);
        Positive(failures, section, nameof(postgres.PoolBackground), postgres.PoolBackground);
        Positive(failures, section, nameof(postgres.BreakerOpenSeconds), postgres.BreakerOpenSeconds);
        DelayRange(failures, section, postgres.MinDelayMs, postgres.MaxDelayMs, postgres.MaxRetries);

        var dragonfly = _dragonfly.Value;
        section = DragonflyResilienceOptions.SectionName;
        Positive(failures, section, nameof(dragonfly.OpMs), dragonfly.OpMs);
        Positive(failures, section, nameof(dragonfly.ConnectSeconds), dragonfly.ConnectSeconds);
        Positive(failures, section, nameof(dragonfly.InFlight), dragonfly.InFlight);
        Positive(failures, section, nameof(dragonfly.BreakerOpenSeconds), dragonfly.BreakerOpenSeconds);
        DelayRange(failures, section, dragonfly.MinDelayMs, dragonfly.MaxDelayMs, dragonfly.MaxRetries);

        var ledger = _ledger.Value;
        section = LedgerResilienceOptions.SectionName;
        Positive(failures, section, nameof(ledger.TimeoutSeconds), ledger.TimeoutSeconds);
        Positive(failures, section, nameof(ledger.MaxAttempts), ledger.MaxAttempts);
        Positive(failures, section, nameof(ledger.FirstDelaySeconds), ledger.FirstDelaySeconds);
        Positive(failures, section, nameof(ledger.InFlight), ledger.InFlight);
        Positive(failures, section, nameof(ledger.BreakerOpenSeconds), ledger.BreakerOpenSeconds);
        if (ledger.MaxDelaySeconds < ledger.FirstDelaySeconds)
        {
            failures.Add($"{section}:MaxDelaySeconds must not be below {section}:FirstDelaySeconds.");
        }

        // D-64: the PostgreSQL budget must end before the request does, so a store outage answers 503 and never the 504 of the request timeout.
        if (postgres.BudgetSeconds >= _hosting.Value.RequestTimeoutSeconds)
        {
            failures.Add($"{PostgresResilienceOptions.SectionName}:{nameof(postgres.BudgetSeconds)} must be below Hosting:RequestTimeoutSeconds.");
        }
    }

    private static void Positive(List<string> failures, string section, string key, int value)
    {
        if (value < 1)
        {
            failures.Add($"{section}:{key} must be at least 1.");
        }
    }

    private static void DelayRange(List<string> failures, string section, int minDelayMs, int maxDelayMs, int maxRetries)
    {
        if (maxRetries < 0)
        {
            failures.Add($"{section}:MaxRetries must not be negative.");
        }

        if (minDelayMs < 0 || maxDelayMs < minDelayMs)
        {
            failures.Add($"{section}:MaxDelayMs must not be below {section}:MinDelayMs, which must not be negative.");
        }
    }
}
