using Microsoft.AspNetCore.Http.HttpResults;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.Endpoints;

/// <summary>
/// The read-only settings and workspace endpoints of the web head: conventions (<c>GET /workspace/conventions</c>), the API's effective options
/// (<c>GET /settings/runtime</c>), plugins, network, hooks, automations, and a size-capped text view of the workspace folder (<c>GET /workspace/tree</c>,
/// <c>GET /workspace/file</c>). Every route needs an authenticated caller and, when <c>Api:Workspace:Owners</c> is set, a caller on that list (anyone
/// else gets 404). None of them writes a file, starts a process or sends a request to an upstream.
/// </summary>
internal static class WorkspaceSettingsEndpoints
{
    private const string Tag = "Workspace settings";

    /// <summary>Maps the settings and workspace endpoints.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapWorkspaceSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        Describe(endpoints.MapGet("/workspace/conventions", ConventionsAsync), "Lists the commands, skills, rules, subagents, hooks files and MCP files of the server workspace, with precedence and diagnostics.");
        Describe(endpoints.MapGet("/settings/runtime", Runtime), "Reads the effective limits and options of the API as read-only values.");
        Describe(endpoints.MapGet("/settings/plugins", PluginsAsync), "Reads the plugin connection check and the plugin inventory of the API.");
        Describe(endpoints.MapGet("/settings/network", Network), "Lists the upstreams the API is configured for. The API sends no request, so nothing is checked.");
        Describe(endpoints.MapGet("/hooks", HooksAsync), "Reads the effective hooks, the files they came from, the loading problems and the consent state. No hook is started.");
        Describe(endpoints.MapGet("/automations", AutomationsAsync), "Reads the automations of the host, or reports that none is configured. No run is started.");
        Describe(endpoints.MapGet("/workspace/tree", Tree), "Lists one folder of the server workspace. Secret files and links that leave the workspace are not listed.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        Describe(endpoints.MapGet("/workspace/file", FileAsync), "Reads one UTF-8 text file of the server workspace, up to 1 MiB. Binary and secret files are refused.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return endpoints;
    }

    private static RouteHandlerBuilder Describe(RouteHandlerBuilder builder, string summary) =>
        builder
            .WithTags(Tag)
            .WithSummary(summary)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(WorkspaceSettingsRateLimiterPolicy.PolicyName);

    private static async Task<Results<Ok<ConventionsResponse>, ProblemHttpResult>> ConventionsAsync(
        WorkspaceAccess access,
        ConventionsReader reader,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        return TypedResults.Ok(await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
    }

    private static Results<Ok<RuntimeSettingsResponse>, ProblemHttpResult> Runtime(WorkspaceAccess access, RuntimeSettingsReader reader, IUserContext user)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        return TypedResults.Ok(reader.Read());
    }

    private static async Task<Results<Ok<PluginsSettingsResponse>, ProblemHttpResult>> PluginsAsync(
        WorkspaceAccess access,
        PluginsSettingsReader reader,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        return TypedResults.Ok(await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
    }

    private static Results<Ok<NetworkSettingsResponse>, ProblemHttpResult> Network(WorkspaceAccess access, NetworkSettingsReader reader, IUserContext user)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        return TypedResults.Ok(reader.Read());
    }

    private static async Task<Results<Ok<HooksResponse>, ProblemHttpResult>> HooksAsync(
        WorkspaceAccess access,
        HooksReader reader,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        return TypedResults.Ok(await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async Task<Results<Ok<AutomationsResponse>, ProblemHttpResult>> AutomationsAsync(
        WorkspaceAccess access,
        AutomationsReader reader,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        return TypedResults.Ok(await reader.ReadAsync(cancellationToken).ConfigureAwait(false));
    }

    private static Results<Ok<WorkspaceTreeResponse>, ProblemHttpResult> Tree(string? path, WorkspaceAccess access, WorkspaceTreeReader reader, IUserContext user)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        var outcome = reader.Read(path);
        if (outcome.Problem is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(outcome.Value!);
    }

    private static async Task<Results<Ok<WorkspaceFileResponse>, ProblemHttpResult>> FileAsync(
        string? path,
        WorkspaceAccess access,
        WorkspaceFileReader reader,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        if (!access.Permits(user.Name))
        {
            return WorkspaceAccess.NotPermitted();
        }

        var outcome = await reader.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (outcome.Problem is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(outcome.Value!);
    }
}
