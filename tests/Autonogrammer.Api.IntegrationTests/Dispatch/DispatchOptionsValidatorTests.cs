using Autonogrammer.Api.Dispatch;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Acp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Verifies dispatch option refusals, the disabled middleware and the security-scheme transformer.</summary>
public sealed class DispatchOptionsValidatorTests
{
    [Fact]
    public void A_default_section_is_valid_and_dispatch_is_off()
    {
        var options = new DispatchOptions();

        options.Enabled.ShouldBeFalse();
        options.ToString().ShouldNotContain("Pem", Case.Sensitive);
        Validator(Environments.Development).Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Nothing_else_is_checked_while_dispatch_is_off()
    {
        var options = new DispatchOptions { Enabled = false, Credentials = { Lifetime = TimeSpan.FromDays(200) }, MaxPayloadBytes = 0 };

        Validator(Environments.Production, mode: ApiAuthMode.OwnerToken).Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void OwnerToken_mode_with_dispatch_enabled_names_only_the_keys()
    {
        var result = Validator(Environments.Development, mode: ApiAuthMode.OwnerToken).Validate(null, Enabled());

        result.Failed.ShouldBeTrue();
        result.Failures!.ShouldContain(item => item.Contains("Dispatch:Enabled", StringComparison.Ordinal) && item.Contains("Api:Auth:Mode", StringComparison.Ordinal));
        string.Join(' ', result.Failures!).ShouldNotContain("secret", Case.Insensitive);
    }

    [Theory]
    [InlineData(91, "Dispatch:Credentials:Lifetime")]
    public void A_lifetime_over_90_days_is_refused(int days, string key)
    {
        var options = Enabled();
        options.Credentials.Lifetime = TimeSpan.FromDays(days);

        var failures = Validator(Environments.Development).Validate(null, options).Failures!;
        failures.ShouldContain(item => item.Contains(key, StringComparison.Ordinal));
        string.Join(' ', failures).ShouldNotContain("BEGIN", Case.Sensitive);
    }

    [Fact]
    public void Queue_skew_and_payload_caps_are_refused()
    {
        var options = Enabled();
        options.QueueTtl = TimeSpan.FromMinutes(6);
        options.ClockSkew = TimeSpan.FromMinutes(3);
        options.MaxPayloadBytes = 262_145;

        var failures = Validator(Environments.Development).Validate(null, options).Failures!.ToArray();

        failures.ShouldContain(item => item.Contains("Dispatch:QueueTtl", StringComparison.Ordinal));
        failures.ShouldContain(item => item.Contains("Dispatch:ClockSkew", StringComparison.Ordinal));
        failures.ShouldContain(item => item.Contains("Dispatch:MaxPayloadBytes", StringComparison.Ordinal));
    }

    [Fact]
    public void Multi_replica_without_Dragonfly_is_refused()
    {
        var options = Enabled();
        options.ReplicaMode = DispatchOptions.DispatchReplicaMode.Multi;

        var failures = Validator(Environments.Development).Validate(null, options).Failures!.ToArray();
        failures.ShouldContain(item => item.Contains("Dispatch:ReplicaMode", StringComparison.Ordinal) && item.Contains("Coordination:Store", StringComparison.Ordinal));
        string.Join(' ', failures).ShouldNotContain("password", Case.Insensitive);
    }

    [Fact]
    public void In_memory_coordination_outside_Development_is_refused()
    {
        var result = Validator(Environments.Production).Validate(null, Enabled());

        result.Failures!.ShouldContain(item => item.Contains("Coordination:Store", StringComparison.Ordinal));
        result.Failures!.ShouldContain("Dispatch:Store must be Postgres outside Development.", StringComparer.Ordinal);
    }

    [Fact]
    public void Outside_Development_without_AllowProduction_is_refused()
    {
        var options = ProductionReady();
        options.AllowProduction = false;

        var result = Validator(Environments.Production, settings: ProductionConfig()).Validate(null, options);

        result.Failures!.ShouldContain("Dispatch:AllowProduction must be true outside Development to start dispatch.", StringComparer.Ordinal);
    }

    [Fact]
    public void Outside_Development_an_in_memory_dispatch_store_is_refused()
    {
        var options = ProductionReady();
        options.Store = DispatchOptions.DispatchStoreKind.InMemory;

        var result = Validator(Environments.Production, settings: ProductionConfig()).Validate(null, options);

        result.Failures!.ShouldContain("Dispatch:Store must be Postgres outside Development.", StringComparer.Ordinal);
    }

    [Fact]
    public void Outside_Development_a_non_Postgres_session_store_is_refused()
    {
        var config = ProductionConfig();
        config["Sessions:Store"] = "InMemory";

        var result = Validator(Environments.Production, settings: config).Validate(null, ProductionReady());

        result.Failures!.ShouldContain("Dispatch:Enabled requires Sessions:Store=Postgres outside Development.", StringComparer.Ordinal);
    }

    [Fact]
    public void Outside_Development_a_missing_Dragonfly_connection_string_is_refused()
    {
        var config = ProductionConfig();
        config.Remove("Coordination:Dragonfly:ConnectionString");

        var result = Validator(Environments.Production, settings: config).Validate(null, ProductionReady());

        result.Failures!.ShouldContain("Dispatch:Enabled requires Coordination:Dragonfly:ConnectionString outside Development.", StringComparer.Ordinal);
    }

    [Fact]
    public void Outside_Development_a_missing_tenant_hash_pepper_is_refused()
    {
        var config = ProductionConfig();
        config.Remove("DataPlane:TenantHashPepper");

        var result = Validator(Environments.Production, settings: config).Validate(null, ProductionReady());

        result.Failures!.ShouldContain("DataPlane:TenantHashPepper is required outside Development.", StringComparer.Ordinal);
    }

    [Fact]
    public void Outside_Development_a_non_Single_replica_mode_is_refused()
    {
        var options = ProductionReady();
        options.ReplicaMode = DispatchOptions.DispatchReplicaMode.Multi;

        var result = Validator(Environments.Production, settings: ProductionConfig()).Validate(null, options);

        result.Failures!.ShouldContain("Dispatch:ReplicaMode must be Single outside Development.", StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Identical_signing_keys_are_refused_in_every_environment(string environment)
    {
        var pem = Pem();
        var options = Enabled();
        options.Credentials.SigningKeyPem = pem;
        options.CommandSigning.KeyPem = pem;

        var result = Validator(environment, settings: ProductionConfig()).Validate(null, options);

        result.Failures!.ShouldContain("Dispatch:Credentials:SigningKeyPem and Dispatch:CommandSigning:KeyPem must be distinct keys.", StringComparer.Ordinal);
    }

    [Fact]
    public void A_production_shaped_dispatch_with_every_requirement_present_passes_validation()
    {
        var result = Validator(Environments.Production, settings: ProductionConfig()).Validate(null, ProductionReady());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Outside_Development_a_missing_hub_origin_is_refused()
    {
        var result = Validator(Environments.Production).Validate(null, Enabled());

        result.Failures!.ShouldContain(item => item.Contains("Dispatch:HubOrigin is required outside Development", StringComparison.Ordinal));
    }

    [Fact]
    public void Outside_Development_a_plain_http_hub_origin_is_refused()
    {
        var options = Enabled();
        options.HubOrigin = "http://hub.example";

        var result = Validator(Environments.Production).Validate(null, options);

        result.Failures!.ShouldContain(item => item.Contains("https outside Development", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hub_origin_with_a_path_is_refused_in_Development()
    {
        var options = Enabled();
        options.HubOrigin = "http://localhost:5000/hub";

        var result = Validator(Environments.Development).Validate(null, options);

        result.Failures!.ShouldContain(item => item.Contains("Dispatch:HubOrigin must be an absolute origin", StringComparison.Ordinal));
    }

    [Fact]
    public void An_http_origin_with_no_path_is_accepted_in_Development()
    {
        var options = Enabled();
        options.HubOrigin = "http://localhost:5000";

        var result = Validator(Environments.Development).Validate(null, options);

        (result.Failures ?? Array.Empty<string>()).Any(item => item.Contains("HubOrigin", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task Disabled_middleware_answers_404_dispatch_disabled()
    {
        var called = false;
        var middleware = new DispatchDisabledMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(DispatchRouteMarker.Instance), "dispatch"));

        await middleware.InvokeAsync(context, Microsoft.Extensions.Options.Options.Create(new DispatchOptions())).ConfigureAwait(true);

        called.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        body.ShouldContain("/problems/dispatch_disabled", Case.Sensitive);
    }

    [Fact]
    public async Task The_transformer_lists_DispatchDevice_on_the_connect_operation_only()
    {
        var document = new OpenApiDocument { Paths = new OpenApiPaths() };
        document.Paths["/dispatch/devices"] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Post] = new() },
        };
        document.Paths["/dispatch/devices/{id}/connect"] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = new() },
        };

        await new DispatchSecuritySchemeTransformer().TransformAsync(document, null!, TestContext.Current.CancellationToken).ConfigureAwait(true);

        document.Components!.SecuritySchemes!.ShouldContainKey("DispatchDevice");
        document.Paths["/dispatch/devices"].Operations![HttpMethod.Post].Security.ShouldBeNull();
        document.Paths["/dispatch/devices/{id}/connect"].Operations![HttpMethod.Get].Security.ShouldNotBeNull().ShouldNotBeEmpty();
    }

    [Fact]
    public void The_default_engine_allow_list_refuses_the_dispatch_key_names()
    {
        var policy = new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist);

        policy.IsAllowed("Dispatch__Credentials__SigningKeyPem").ShouldBeFalse();
        policy.IsAllowed("Dispatch__CommandSigning__KeyPem").ShouldBeFalse();
    }

    [Fact]
    public void The_test_auth_handler_can_be_constructed()
    {
        var handler = new DispatchTestAuthHandler(new Fixed(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, System.Text.Encodings.Web.UrlEncoder.Default);
        handler.ShouldNotBeNull();
    }

    private sealed class Fixed : Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
    {
        public Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions CurrentValue { get; } = new();

        public Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, string?> listener) => new Noop();
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static DispatchOptions Enabled() => new()
    {
        Enabled = true,
        Credentials = { SigningKeyPem = Pem(), Lifetime = TimeSpan.FromDays(30) },
        CommandSigning = { KeyPem = Pem() },
    };

    private static string Pem()
    {
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return key.ExportPkcs8PrivateKeyPem();
    }

    private static DispatchOptions ProductionReady()
    {
        var options = Enabled();
        options.AllowProduction = true;
        options.Store = DispatchOptions.DispatchStoreKind.Postgres;
        options.HubOrigin = "https://hub.example";
        return options;
    }

    private static Dictionary<string, string?> ProductionConfig() => new(StringComparer.Ordinal)
    {
        ["Coordination:Store"] = "Dragonfly",
        ["Coordination:Dragonfly:ConnectionString"] = "dragonfly.example:6379",
        ["Sessions:Store"] = "Postgres",
        ["Sessions:Postgres:ConnectionString"] = "Host=postgres.example;Database=autonogrammer",
        ["DataPlane:TenantHashPepper"] = "test-pepper",
    };

    private static DispatchOptionsValidator Validator(string environment, ApiAuthMode mode = ApiAuthMode.Oidc, Dictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Coordination:Store"] = "InMemory" };
        if (settings is not null)
        {
            foreach (var pair in settings)
            {
                values[pair.Key] = pair.Value;
            }
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new DispatchOptionsValidator(new TestEnvironment(environment), configuration, Microsoft.Extensions.Options.Options.Create(new ApiAuthOptions { Mode = mode }));
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public TestEnvironment(string name) => EnvironmentName = name;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
