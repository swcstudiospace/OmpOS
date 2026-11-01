using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.Options;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>The data-plane options fail startup with messages that name a key and never repeat a value.</summary>
public sealed class DataPlaneOptionsValidatorTests
{
    private const string SecretPepper = "pepper-secret-value-9f3c";
    private const string SecretToken = "ledger-token-secret-77ab";

    /// <summary>Verifies the defaults are valid for an in-memory deployment.</summary>
    [Fact]
    public void Defaults_AreValid()
    {
        Validate(new DataPlaneOptions()).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies the Dragonfly store needs the pepper, naming the key.</summary>
    [Fact]
    public void DragonflyStore_WithoutPepper_NamesTheKey()
    {
        var result = Validate(new DataPlaneOptions(), store: CoordinationOptions.DragonflyStore);

        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(static message => message.Contains("DataPlane:TenantHashPepper", StringComparison.Ordinal));
    }

    /// <summary>Verifies a pepper satisfies the Dragonfly rule and its value never appears in a message.</summary>
    [Fact]
    public void DragonflyStore_WithPepper_IsValid_AndNoMessageRepeatsIt()
    {
        var options = new DataPlaneOptions { TenantHashPepper = SecretPepper };

        Validate(options, store: CoordinationOptions.DragonflyStore).Succeeded.ShouldBeTrue();
        var broken = new DataPlaneOptions { TenantHashPepper = SecretPepper, Retention = { AuditDays = 10 } };
        Validate(broken, store: CoordinationOptions.DragonflyStore).Failures!.ShouldNotContain(static message => message.Contains(SecretPepper, StringComparison.Ordinal));
    }

    /// <summary>Verifies an audit retention below the floor is refused, whatever floor is configured.</summary>
    [Theory]
    [InlineData(89, 90)]
    [InlineData(100, 120)]
    [InlineData(400, 80)]
    public void AuditRetention_BelowTheFloor_IsRefused(int auditDays, int floorDays)
    {
        var result = Validate(new DataPlaneOptions { Retention = { AuditDays = auditDays, AuditFloorDays = floorDays } });

        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(static message => message.Contains("DataPlane:Retention:Audit", StringComparison.Ordinal));
    }

    /// <summary>Verifies the schema wait accepts 1 to 600 seconds only.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(120, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void SchemaWait_IsBoundedTo1Through600(int seconds, bool valid)
    {
        var result = Validate(new DataPlaneOptions { Postgres = { SchemaWaitSeconds = seconds } });

        result.Succeeded.ShouldBe(valid);
        if (!valid)
        {
            result.Failures!.ShouldContain(static message => message.Contains("DataPlane:Postgres:SchemaWaitSeconds", StringComparison.Ordinal));
        }
    }

    /// <summary>Verifies the PostgreSQL budget must end before the request timeout (D-64), naming both keys.</summary>
    [Fact]
    public void PostgresBudget_NotBelowRequestTimeout_IsRefused()
    {
        var result = Validate(new DataPlaneOptions(), requestTimeoutSeconds: 8);

        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(static message =>
            message.Contains("DataPlane:Postgres:Resilience:BudgetSeconds", StringComparison.Ordinal) && message.Contains("Hosting:RequestTimeoutSeconds", StringComparison.Ordinal));
        Validate(new DataPlaneOptions(), requestTimeoutSeconds: 9).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies a disabled ledger needs neither an endpoint nor a token.</summary>
    [Fact]
    public void Ledger_Disabled_NeedsNothing()
    {
        Validate(new DataPlaneOptions { Ledger = { Enabled = false, Endpoint = "not a url" } }).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies an enabled ledger needs a valid endpoint and a token, and no message repeats the token or the endpoint.</summary>
    [Fact]
    public void Ledger_Enabled_NeedsEndpointAndToken()
    {
        var missing = Validate(new DataPlaneOptions { Ledger = { Enabled = true } });
        missing.Failures!.ShouldContain(static message => message.Contains("DataPlane:Ledger:Endpoint", StringComparison.Ordinal));
        missing.Failures!.ShouldContain(static message => message.Contains("DataPlane:Ledger:Token", StringComparison.Ordinal));

        const string leakyEndpoint = "http://user:endpoint-password-31@ledger.example.test/";
        var invalid = Validate(new DataPlaneOptions { Ledger = { Enabled = true, Endpoint = leakyEndpoint, Token = SecretToken } });
        invalid.Failed.ShouldBeTrue();
        invalid.Failures!.ShouldContain(static message => message.Contains("DataPlane:Ledger:Endpoint", StringComparison.Ordinal));
        invalid.Failures!.ShouldAllBe(static message =>
            !message.Contains(SecretToken, StringComparison.Ordinal) && !message.Contains("endpoint-password-31", StringComparison.Ordinal));

        Validate(new DataPlaneOptions { Ledger = { Enabled = true, Endpoint = "https://ledger.example.test/", Token = SecretToken } }).Succeeded.ShouldBeTrue();
        Validate(new DataPlaneOptions { Ledger = { Enabled = true, Endpoint = "http://127.0.0.1:9000/", Token = SecretToken } }).Succeeded.ShouldBeTrue();
    }

    /// <summary>Verifies an out-of-range resilience value names its key.</summary>
    [Fact]
    public void ResilienceValue_OutOfRange_NamesTheKey()
    {
        var result = Validate(new DataPlaneOptions(), postgres: new PostgresResilienceOptions { PoolInteractive = 0 });

        result.Failures!.ShouldContain(static message => message.Contains("DataPlane:Postgres:Resilience:PoolInteractive", StringComparison.Ordinal));
    }

    private static ValidateOptionsResult Validate(
        DataPlaneOptions options,
        string store = CoordinationOptions.InMemoryStore,
        int requestTimeoutSeconds = 30,
        PostgresResilienceOptions? postgres = null)
    {
        var validator = new DataPlaneOptionsValidator(
            MsOptions.Create(new CoordinationOptions { Store = store }),
            MsOptions.Create(new HostingOptions { RequestTimeoutSeconds = requestTimeoutSeconds }),
            MsOptions.Create(postgres ?? new PostgresResilienceOptions()),
            MsOptions.Create(new DragonflyResilienceOptions()),
            MsOptions.Create(new LedgerResilienceOptions()));
        return validator.Validate(null, options);
    }
}
