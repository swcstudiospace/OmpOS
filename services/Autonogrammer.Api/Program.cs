using Autonogrammer.Api.Automations;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Coordination;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.Health;
using Autonogrammer.Api.Metrics;
using Autonogrammer.Api.Middleware;
using Autonogrammer.Api.OpenApi;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Persistence;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Serialization;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Services.Providers;
using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Api.Sse;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Api.Workflows;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Parser;
using Autonogrammer.Sandbox;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.SessionRuns;
using Autonogrammer.Sessions;
using Autonogrammer.Workflows;
using Autonogrammer.Worktrees;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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
// Harness:Adapter selects the harness. Outside Development the in-memory echo adapter is refused: the hosted path runs omp over ACP.
var harnessInDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddOptions<HarnessOptions>()
    .BindConfiguration(HarnessOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(static options => HarnessOptions.IsSupportedAdapter(options.Adapter), "Harness:Adapter must be InMemory or Acp.")
    .Validate(options => harnessInDevelopment || !HarnessOptions.IsInMemoryAdapter(options.Adapter), "Harness:Adapter must be Acp outside the Development environment; InMemory is a development-only adapter.")
    .Validate(static options => !HarnessOptions.IsAcpAdapter(options.Adapter) || !string.IsNullOrWhiteSpace(options.Acp.OmpPath), "Harness:Acp:OmpPath must be set when Harness:Adapter is Acp.")
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
// Worktrees: the operator-controlled root for per-run git worktrees and the repository they come from. Unset values are allowed; a set value
// must be an absolute path, so a relative root or repository stops startup and the error names the key.
builder.Services.AddOptions<WorktreesOptions>()
    .BindConfiguration(WorktreesOptions.SectionName)
    .Validate(static options => string.IsNullOrWhiteSpace(options.Root) || Path.IsPathFullyQualified(options.Root), "Worktrees:Root must be an absolute path when set.")
    .Validate(static options => string.IsNullOrWhiteSpace(options.Repository) || Path.IsPathFullyQualified(options.Repository), "Worktrees:Repository must be an absolute path when set.")
    .ValidateOnStart();

// OIDC bearer sign-in (OD-03). ApiAuthOptionsValidator checks these options against the host environment on start: outside
// Development, Authority and Audience are required, Authority must be https, and the development principal is refused.
builder.Services.AddOptions<ApiAuthOptions>()
    .BindConfiguration(ApiAuthOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ApiAuthOptions>, ApiAuthOptionsValidator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerSetup>();
// Default policy: every endpoint without an explicit policy requires an authenticated user. Health and (in Development) OpenAPI opt out below.
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Source-generation-only JSON: API DTOs plus every contract shape.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AutonogrammerJsonContext.Default);
});

// Session state and prompt execution (in-memory adapter; no model calls).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApiSessionStore(builder.Configuration);
builder.Services.AddApiCoordination(builder.Configuration);
builder.Services.AddSingleton<SessionService>();
builder.Services.AddOmpParser();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<RunRegistry>();
// Harness:Adapter picks the factory. Acp runs omp over stdio through OmpAcpClient, which takes its permission resolver from DI;
// InMemory is the development echo adapter.
builder.Services.AddSingleton<OmpAcpClient>(static services => new OmpAcpClient(services.GetService<IPermissionResolver>()));
builder.Services.AddSingleton<AcpHarnessSessionFactory>();
builder.Services.AddSingleton<InMemoryHarnessSessionFactory>();
builder.Services.AddSingleton<IHarnessSessionFactory>(static services =>
{
    var adapter = services.GetRequiredService<IOptions<HarnessOptions>>().Value.Adapter;
    if (HarnessOptions.IsAcpAdapter(adapter))
    {
        return services.GetRequiredService<AcpHarnessSessionFactory>();
    }

    return services.GetRequiredService<InMemoryHarnessSessionFactory>();
});
builder.Services.AddSingleton<PromptRunner>();
builder.Services.AddSessionWebServices();
builder.Services.AddCloudAgentServices();
builder.Services.AddProviderServices();
builder.Services.AddWorkspaceSettingsServices();
builder.Services.AddAutomationServices();

// The API is the composition root for session runs. Each prompt runs in this process through DirectSessionRunner, which holds
// the session's run lease from the coordination store for the whole run. The sandbox is the in-memory fake provider.
builder.Services.AddSingleton<ISandboxProvider, FakeSandboxProvider>();
builder.Services.AddSingleton<ISessionRunObserver, ApiSessionRunObserver>();
// The activities are wrapped so a hosted run (POST /sessions/{id}/runs) reports when its harness starts the prompt.
builder.Services.AddSingleton<SessionRunActivities>();
builder.Services.AddSingleton<ISessionRunActivities>(static services => new RunTrackingActivities(
    services.GetRequiredService<SessionRunActivities>(),
    services.GetRequiredService<RunRegistry>()));
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
// The caller is the subject of the validated bearer token. The development principal is used only behind the explicit
// Api:Auth:AllowDevelopmentPrincipal opt-in, which the options validator refuses outside Development.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext>(static services =>
{
    if (services.GetRequiredService<IOptions<ApiAuthOptions>>().Value.AllowDevelopmentPrincipal)
    {
        return new SingleDevPrincipal();
    }

    return new AuthenticatedUserContext(services.GetRequiredService<IHttpContextAccessor>());
});

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

// Each limiter policy is a keyed IRateLimiterPolicy<string>, resolved when the limiter options are built. The session policies are the
// defaults; registering a different policy under the same key substitutes the limiter for that endpoint group.
builder.Services.AddKeyedSingleton<IRateLimiterPolicy<string>, SessionRateLimiterPolicy>(SessionEndpoints.PromptsPolicy);
builder.Services.AddKeyedSingleton<IRateLimiterPolicy<string>, SessionCreationRateLimiterPolicy>(SessionEndpoints.CreationPolicy);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, services) =>
{
    options.AddPolicy(SessionEndpoints.PromptsPolicy, services.GetRequiredKeyedService<IRateLimiterPolicy<string>>(SessionEndpoints.PromptsPolicy));
    options.AddPolicy(SessionEndpoints.CreationPolicy, services.GetRequiredKeyedService<IRateLimiterPolicy<string>>(SessionEndpoints.CreationPolicy));
});

builder.Services.AddHealthChecks()
    .AddCheck("self", static _ => HealthCheckResult.Healthy("Live."), tags: ["live"])
    .AddCheck<HarnessReadinessCheck>("harness", tags: ["ready"])
    .AddCheck<SessionRepositoryCheck>("repository", tags: ["ready"]);

builder.Services.AddAutonogrammerOpenApi();

var otelExporter = builder.Configuration.GetValue<string>("Otel:Exporter") ?? "none";
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddSource(ApiTelemetry.SourceName);
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
        metrics.AddAspNetCoreInstrumentation().AddMeter(ApiTelemetry.SourceName);
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
app.UseCors(ConfigureCorsOptions.PolicyName);
app.UseAuthentication();
var authOptions = app.Services.GetRequiredService<IOptions<ApiAuthOptions>>().Value;
if (authOptions.AllowDevelopmentPrincipal)
{
    app.UseMiddleware<AuthBoundaryMiddleware>();
}

app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<CloudDisabledMiddleware>();
app.UseMiddleware<AutomationDisabledMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference().AllowAnonymous();
}

// OpenAPI document is anonymous in Development only; elsewhere the fallback policy requires a token.
var openApi = app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    openApi.AllowAnonymous();
}
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = static check => check.Tags.Contains("live") }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
}).AllowAnonymous();
app.MapSessionEndpoints();
app.MapToolApprovalEndpoints();
app.MapEventStreamEndpoints();
app.MapRunEndpoints();
app.MapCatalogEndpoints();
app.MapCatalogEventEndpoints();
app.MapStageEndpoints();
app.MapSteeringEndpoints();
app.MapSessionConfigEndpoints();
app.MapChangeReviewEndpoints();
app.MapSourceEndpoints();
app.MapWorkspaceSettingsEndpoints();
app.MapProviderEndpoints();
app.MapCloudAgentEndpoints();
app.MapAutomationEndpoints();

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
