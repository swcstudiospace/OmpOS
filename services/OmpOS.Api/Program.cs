using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OmpOS.Api.Endpoints;
using OmpOS.Api.Harness;
using OmpOS.Api.Health;
using OmpOS.Api.Metrics;
using OmpOS.Api.Middleware;
using OmpOS.Api.OpenApi;
using OmpOS.Api.Options;
using OmpOS.Api.Security;
using OmpOS.Api.Serialization;
using OmpOS.Api.Services;
using OmpOS.Api.Sse;
using OmpOS.Api.Telemetry;
using OmpOS.Api.Workflows;
using OmpOS.Contracts.Serialization;
using OmpOS.Parser;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
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
builder.Services.AddOptions<WorkflowsOptions>().BindConfiguration(WorkflowsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
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
builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddOmpParser();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<IHarnessSessionFactory, InMemoryHarnessSessionFactory>();
builder.Services.AddSingleton<PromptRunner>();

// The API is the composition root for the session-run workflow (WFLW-01). When durable
// workflows are disabled (the default), each prompt runs in-process through DirectSessionRunner
// with an in-memory fake sandbox; the Temporal worker is a separate process and is not wired
// into the API, so nothing is registered here when Workflows:Enabled is true.
if (!builder.Configuration.GetValue<bool>("Workflows:Enabled"))
{
    builder.Services.AddSingleton<ISandboxProvider, FakeSandboxProvider>();
    builder.Services.AddSingleton<ISessionRunActivities, ApiSessionRunActivities>();
    builder.Services.AddSingleton<ISessionRunWorkflow, DirectSessionRunner>();
}
builder.Services.AddSingleton<SessionEventStreamer>();
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
    .AddCheck<SessionRepositoryCheck>("repository", tags: ["ready"])
    .AddCheck<TemporalCheck>("temporal", tags: ["ready"]);

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
