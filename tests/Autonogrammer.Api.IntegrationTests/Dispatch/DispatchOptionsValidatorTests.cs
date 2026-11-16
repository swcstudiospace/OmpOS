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

    private static DispatchOptionsValidator Validator(string environment, ApiAuthMode mode = ApiAuthMode.Oidc)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Coordination:Store"] = "InMemory" }).Build();
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
