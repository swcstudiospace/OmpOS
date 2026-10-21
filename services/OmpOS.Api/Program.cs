using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OmpOS.Api.Coordination;
using OmpOS.Api.Endpoints;
using OmpOS.Api.Harness;
using OmpOS.Api.Health;
using OmpOS.Api.Metrics;
using OmpOS.Api.Middleware;
using OmpOS.Api.OpenApi;
using OmpOS.Api.Options;
using OmpOS.Api.Persistence;
using OmpOS.Api.Security;
using OmpOS.Api.Serialization;
using OmpOS.Api.Services;
using OmpOS.Api.Sse;
using OmpOS.Api.Telemetry;
using OmpOS.Api.Workflows;
using OmpOS.Contracts.Serialization;
using OmpOS.Harness.Abstractions;
using OmpOS.Parser;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
using OmpOS.SessionRuns;
using OmpOS.Sessions;
using OmpOS.Workflows;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Fail-fast options: every section binds with data-annotation validation on start,
// so a missing required key (notably Harness:Adapter) names itself in the startup error.
// Data-annotation validation does not recurse into nested objects, so the nested rate-limit
// section is bound and validated on its own; ApiOptions.RateLimit reads the same values.
builder.Services.AddOptions<ApiOptions>().BindConfiguration(ApiOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ApiRateLimitOptions>().BindConfiguration($"{ApiOptions.SectionName}:RateLimit").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<HarnessOptions>()
    .BindConfiguration(HarnessOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(static options => HarnessOptions.IsSupportedAdapter(options.Adapter), "Harness:Adapter must be InMemory, the only adapter supported in Phase 2.")
    .ValidateOnStart();
builder.Services.AddOptions<SessionsOptions>()
    .BindConfiguration(SessionsOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(static options => string.IsNullOrEmpty(options.Store) || SessionsOptions.IsSupportedStore(options.Store), "Sessions:Store must be Postgres or InMemory.")
    .Validate(static options => !SessionsOptions.IsPostgresStore(options.Store) || !string.IsNullOrWhiteSpace(options.Postgres.ConnectionString), "Sessions:Postgres:ConnectionString is required when Sessions:Store is Postgres.")
    .ValidateOnStart();
builder.Services.AddOptions<CoordinationOptions>()
    .BindConfiguration(CoordinationOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(static options => string.IsNullOrEmpty(options.Store) || CoordinationOptions.IsSupportedStore(options.Store), "Coordination:Store must be Dragonfly or InMemory.")
    .Validate(static options => !CoordinationOptions.IsDragonflyStore(options.Store) || !string.IsNullOrWhiteSpace(options.Dragonfly.ConnectionString), "Coordination:Dragonfly:ConnectionString is required when Coordination:Store is Dragonfly.")
    .ValidateOnStart();
builder.Services.AddOptions<OtelOptions>().BindConfiguration(OtelOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<DebugOptions>().BindConfiguration(DebugOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

// Source-generation-only JSON: API DTOs plus every contract shape.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OmpOsJsonContext.Default);
});

// Session state and prompt execution (in-memory adapter; no model calls).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApiSessionStore(builder.Configuration);
builder.Services.AddApiCoordination(builder.Configuration);
builder.Services.AddSingleton<SessionService>();
builder.Services.AddOmpParser();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<IHarnessSessionFactory, InMemoryHarnessSessionFactory>();
builder.Services.AddSingleton<PromptRunner>();

// The API is the composition root for session runs. Each prompt runs in this process through DirectSessionRunner, which holds
// the session's run lease from the coordination store for the whole run. The sandbox is the in-memory fake provider.
builder.Services.AddSingleton<ISandboxProvider, FakeSandboxProvider>();
builder.Services.AddSingleton<ISessionRunObserver, ApiSessionRunObserver>();
builder.Services.AddSingleton<ISessionRunActivities, SessionRunActivities>();
builder.Services.AddSingleton<ISessionRunWorkflow, DirectSessionRunner>();
builder.Services.AddSingleton<DirectSessionRunDispatcher>();
builder.Services.AddSingleton<SessionEventStreamer>();
builder.Services.AddSingleton<SessionChangePublisher>();
builder.Services.AddSingleton<SessionSignalSubscriber>();
builder.Services.AddSingleton<IHostedService>(static services => services.GetRequiredService<SessionSignalSubscriber>());
// The reaper is always registered. Its sweep reads Coordination:Store at each sweep and does nothing unless the store is Dragonfly,
// where leases are shared between instances; see SessionRunReaper.
builder.Services.AddSingleton<SessionRunReaper>();
builder.Services.AddSingleton<IHostedService>(static services => services.GetRequiredService<SessionRunReaper>());
builder.Services.AddSingleton<ApiMetrics>();
builder.Services.AddScoped<IUserContext, SingleDevPrincipal>();

// CORS locked to Api:Cors:Origins; empty means no cross-origin traffic. The policy is
// built from IOptions on first use (see ConfigureCorsOptions) so late configuration
// sources are honored.
builder.Services.AddCors();
builder.Services.AddSingleton<IConfigureOptions<CorsOptions>, ConfigureCorsOptions>();

// RFC 9457 bodies for unhandled exceptions (Production) and validation faults.
builder.Services.AddProblemDetails(static options =>
{
    // The exception handler writes a title-only 500 by default, which leaves no
    // stable contract for clients. Default the detail to a fixed, internals-free
    // sentence so 500s always carry status/title/detail and never leak.
    options.CustomizeProblemDetails = static context =>
    {
        if (context.ProblemDetails.Status == StatusCodes.Status500InternalServerError
            && context.ProblemDetails.Detail is null)
        {
            context.ProblemDetails.Detail = "An unexpected error occurred.";
        }
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy<string, SessionRateLimiterPolicy>(SessionEndpoints.PromptsPolicy);
    options.AddPolicy<string, SessionCreationRateLimiterPolicy>(SessionEndpoints.CreationPolicy);
});

builder.Services.AddHealthChecks()
    .AddCheck("self", static _ => HealthCheckResult.Healthy("Live."), tags: ["live"])
    .AddCheck<HarnessReadinessCheck>("harness", tags: ["ready"])
    .AddCheck<SessionRepositoryCheck>("repository", tags: ["ready"]);

builder.Services.AddOmpOpenApi();

var otelExporter = builder.Configuration.GetValue<string>("Otel:Exporter") ?? "none";
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddSource(OmpTelemetry.SourceName);
        if (otelExporter.Equals("console", StringComparison.OrdinalIgnoreCase))
        {
            tracing.AddConsoleExporter();
        }
        else if (otelExporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation().AddMeter(OmpTelemetry.SourceName);
        if (otelExporter.Equals("console", StringComparison.OrdinalIgnoreCase))
        {
            metrics.AddConsoleExporter();
        }
        else if (otelExporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
        {
            metrics.AddOtlpExporter();
        }
    });

var app = builder.Build();

// Unhandled exceptions become ProblemDetails 500s outside Development. The developer
// exception page stays Development-only (framework default); Production never leaks internals.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<AuthBoundaryMiddleware>();
app.UseCors(ConfigureCorsOptions.PolicyName);
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapOpenApi();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
});
app.MapSessionEndpoints();
app.MapToolApprovalEndpoints();
app.MapEventStreamEndpoints();

// Start without RunAsync's dispose-on-failure: RunAsync disposes the host when startup
// validation throws, which races WebApplicationFactory's in-memory-host observation
// (DeferredHost.StartAsync resolves IHostApplicationLifetime from the same provider)
// and intermittently surfaces ObjectDisposedException instead of the keyed
// OptionsValidationException. Disposing only after a successful start keeps shutdown
// behavior identical while making startup failures deterministic.
var started = false;
try
{
    app.Start();
    started = true;
    app.WaitForShutdown();
}
finally
{
    if (started)
    {
        ((IDisposable)app).Dispose();
    }
}

/// <summary>
/// The API host entry point. Internal with InternalsVisibleTo for
/// <c>WebApplicationFactory&lt;Program&gt;</c> integration tests.
/// </summary>
partial class Program
{
}
