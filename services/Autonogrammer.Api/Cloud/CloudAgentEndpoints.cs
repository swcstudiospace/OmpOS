using System.Diagnostics;
using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Idempotency;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Contracts.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The cloud agent routes. They are always mapped, so the OpenAPI document describes them; while <c>Cloud:Enabled</c> is false the <see cref="CloudDisabledMiddleware"/>
/// answers 404 <c>cloud_disabled</c> for every one of them before any handler runs. Every route is
/// owner-scoped: an agent of another owner reads as absent. The routes follow the Cursor Cloud Agents API v1 where the model fits, and the event stream
/// carries the same event kinds as <c>GET /sessions/{id}/events</c>. Problems carry a stable code in <c>type</c> (<c>/problems/{code}</c>).
/// </summary>
internal static class CloudAgentEndpoints
{
    /// <summary>Maps the cloud agent routes, once per host. A second call maps nothing.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCloudAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!endpoints.ServiceProvider.GetRequiredService<CloudEndpointsMapped>().TryMark())
        {
            return endpoints;
        }

        var routes = endpoints.MapGroup(string.Empty).WithMetadata(CloudRouteMarker.Instance);
        routes.MapPost("/cloud/agents", CreateAsync)
            .WithTags("Cloud")
            .WithSummary("Creates a cloud agent bound to repositories, a branch and secret references, and starts provisioning its worker.")
            .Accepts<CreateCloudAgentRequest>("application/json")
            .Produces<CloudAgentResponse>(StatusCodes.Status202Accepted)
            .Produces<CloudAgentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.CreationPolicy);
        routes.MapGet("/cloud/agents", ListAsync)
            .WithTags("Cloud")
            .WithSummary("Lists the caller's cloud agents, newest first.")
            .Produces<CloudAgentListResponse>();
        routes.MapGet("/cloud/agents/{id}", GetAsync)
            .WithTags("Cloud")
            .WithSummary("Reads one cloud agent.")
            .Produces<CloudAgentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        routes.MapPost("/cloud/agents/{id}/prompts", PromptAsync)
            .WithTags("Cloud")
            .WithSummary("Sends a prompt to a cloud agent. The run executes on the agent's worker; its events stream from /cloud/agents/{id}/events.")
            .Accepts<CloudPromptRequest>("application/json")
            .Produces<CloudPromptAccepted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(SessionEndpoints.PromptsPolicy);
        _ = MapLifecycleEndpoints(routes);
        return endpoints;
    }

    private static IEndpointRouteBuilder MapLifecycleEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cloud/agents/{id}/cancel", CancelAsync)
            .WithTags("Cloud")
            .WithSummary("Cancels the run in flight on this server instance.")
            .Produces<CloudAgentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        routes.MapPost("/cloud/agents/{id}/stop", StopAsync)
            .WithTags("Cloud")
            .WithSummary("Stops a cloud agent: cancels its run and lets its worker sleep. A wake request starts it again.")
            .Produces<CloudAgentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        routes.MapPost("/cloud/agents/{id}/wake", WakeAsync)
            .WithTags("Cloud")
            .WithSummary("Wakes an idle, stopped, failed or orphaned cloud agent. The worker comes up in the background; poll the agent until it is running.")
            .Produces<CloudAgentResponse>(StatusCodes.Status202Accepted)
            .Produces<CloudAgentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        routes.MapDelete("/cloud/agents/{id}", DeleteAsync)
            .WithTags("Cloud")
            .WithSummary("Deletes a cloud agent and removes its worker and volume. The session log stays.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);
        routes.MapGet("/cloud/agents/{id}/events", EventsAsync)
            .WithTags("Cloud")
            .WithSummary("Streams the agent's events: the same kinds, ids and resume rules as GET /sessions/{id}/events.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
        routes.MapPost("/cloud/agents/{id}/pull-request", PullRequestAsync)
            .WithTags("Cloud")
            .WithSummary("Opens a pull request from the agent's branch. It is ready for review unless a draft is requested and allowed.")
            .Accepts<CreateCloudPullRequestRequest>("application/json")
            .Produces<CloudPullRequest>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return MapSecretEndpoints(routes);
    }

    private static IEndpointRouteBuilder MapSecretEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cloud/secrets", ListSecretsAsync)
            .WithTags("Cloud")
            .WithSummary("Lists the caller's secrets by name. Values are never returned.")
            .Produces<CloudSecretListResponse>();
        routes.MapGet("/cloud/secrets/{name}", GetSecretAsync)
            .WithTags("Cloud")
            .WithSummary("Tells whether a secret exists and when it was written. The value is write-only.")
            .Produces<CloudSecretInfo>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        routes.MapPut("/cloud/secrets/{name}", PutSecretAsync)
            .WithTags("Cloud")
            .WithSummary("Stores or replaces a secret. It reaches an agent's environment only at provisioning and is redacted from output.")
            .Accepts<CloudSecretValueRequest>("application/json")
            .Produces<CloudSecretInfo>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        routes.MapDelete("/cloud/secrets/{name}", DeleteSecretAsync)
            .WithTags("Cloud")
            .WithSummary("Deletes a secret. Agents that were already provisioned keep the value in their environment until they are deleted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
        routes.MapGet("/cloud/limits", LimitsAsync)
            .WithTags("Cloud")
            .WithSummary("Reports the caller's caps and how much of them is used.")
            .Produces<CloudLimitsResponse>();
        return routes;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        CloudAgentService service,
        IUserContext user,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        if (!IdempotencyKeyHeader.TryRead(context.Request.Headers, out var key))
        {
            return Problem(CloudProblem.BadRequest("invalid_idempotency_key", "Invalid idempotency key.", $"The {IdempotencyKeyHeader.HeaderName} header must appear at most once and hold 1 to {IdempotencyKeyHeader.MaxLength.ToString(CultureInfo.InvariantCulture)} visible ASCII characters."));
        }

        var body = await WebBodyReader.ReadAsync(context, options.Value, CloudJsonContext.Default.CreateCloudAgentRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var outcome = await service.CreateAsync(user.Name, body.Value!, key, cancellationToken).ConfigureAwait(false);
        if (outcome.Problem is not null)
        {
            return Problem(outcome.Problem);
        }

        var created = outcome.Value!;
        if (created.Replayed)
        {
            context.Response.Headers["Idempotency-Replayed"] = "true";
            return TypedResults.Ok(created.Agent);
        }

        return TypedResults.Accepted($"/cloud/agents/{created.Agent.Id}", created.Agent);
    }

    private static async Task<IResult> ListAsync(CloudAgentService service, IUserContext user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.ListAsync(user.Name, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> GetAsync(string id, CloudAgentService service, IUserContext user, CancellationToken cancellationToken) =>
        Respond(await service.GetAsync(user.Name, id, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> PromptAsync(
        HttpContext context,
        string id,
        CloudAgentService service,
        IUserContext user,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        using var activity = ApiTelemetry.Activities.StartActivity("POST /cloud/agents/{id}/prompts", ActivityKind.Server);
        activity?.SetTag("autonogrammer.cloud.agent.id", id);
        var body = await WebBodyReader.ReadAsync(context, options.Value, CloudJsonContext.Default.CloudPromptRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var prompt = body.Value!.Prompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Problem(CloudProblem.BadRequest("invalid_prompt", "Invalid prompt.", "The prompt must not be empty."));
        }

        if (prompt.Length > options.Value.MaxPromptLength)
        {
            return Problem(CloudProblem.BadRequest("invalid_prompt", "Invalid prompt.", $"The prompt exceeds the maximum length of {options.Value.MaxPromptLength.ToString(CultureInfo.InvariantCulture)} characters."));
        }

        var outcome = await service.PromptAsync(user.Name, id, prompt, context.TraceIdentifier, activity?.Context, cancellationToken).ConfigureAwait(false);
        return outcome.Problem is not null
            ? Problem(outcome.Problem)
            : TypedResults.Accepted($"/cloud/agents/{id}/events", outcome.Value);
    }

    private static async Task<IResult> CancelAsync(string id, CloudAgentService service, IUserContext user, CancellationToken cancellationToken) =>
        Respond(await service.CancelAsync(user.Name, id, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> StopAsync(string id, CloudAgentService service, IUserContext user, CancellationToken cancellationToken) =>
        Respond(await service.StopAsync(user.Name, id, cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> WakeAsync(string id, CloudAgentService service, IUserContext user, CancellationToken cancellationToken)
    {
        var outcome = await service.WakeAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        if (outcome.Problem is not null)
        {
            return Problem(outcome.Problem);
        }

        return outcome.Value!.Status == CloudAgentStatus.Starting
            ? (IResult)TypedResults.Accepted($"/cloud/agents/{id}", outcome.Value)
            : TypedResults.Ok(outcome.Value);
    }

    private static async Task<IResult> DeleteAsync(string id, CloudAgentService service, IUserContext user, CancellationToken cancellationToken)
    {
        var outcome = await service.DeleteAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        return outcome.Problem is not null ? Problem(outcome.Problem) : TypedResults.NoContent();
    }

    private static async Task<IResult> PullRequestAsync(
        HttpContext context,
        string id,
        CloudAgentService service,
        IUserContext user,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        var body = await WebBodyReader.ReadAsync(context, options.Value, CloudJsonContext.Default.CreateCloudPullRequestRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var outcome = await service.CreatePullRequestAsync(user.Name, id, body.Value!, cancellationToken).ConfigureAwait(false);
        return outcome.Problem is not null
            ? Problem(outcome.Problem)
            : TypedResults.Created($"/cloud/agents/{id}", outcome.Value);
    }

    private static async Task<IResult> EventsAsync(
        HttpContext context,
        string id,
        CloudAgentService service,
        IUserContext user,
        SessionEventStreamer streamer,
        CancellationToken cancellationToken)
    {
        var sessionId = await service.FindSessionIdAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        if (sessionId is null)
        {
            return Problem(CloudProblem.AgentNotFound(id));
        }

        long? header = null;
        if (context.Request.Headers.TryGetValue("Last-Event-ID", out var headerValues) && headerValues.Count > 0 && !string.IsNullOrWhiteSpace(headerValues[0]))
        {
            if (!long.TryParse(headerValues[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return TypedResults.Problem(detail: "Last-Event-ID must be a non-negative integer sequence number.", statusCode: 400, title: "Invalid resume position.");
            }

            header = parsed;
        }

        long? query = null;
        if (context.Request.Query.TryGetValue("since", out var queryValues) && queryValues.Count > 0)
        {
            if (!long.TryParse(queryValues[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return TypedResults.Problem(detail: "The since query value must be a non-negative integer sequence number.", statusCode: 400, title: "Invalid resume position.");
            }

            query = parsed;
        }

        return new SseStreamResult(StreamAsync(sessionId, header ?? query ?? 0, streamer, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> StreamAsync(
        string sessionId,
        long afterSeq,
        SessionEventStreamer streamer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in streamer.SubscribeAsync(sessionId, afterSeq, cancellationToken).ConfigureAwait(false))
        {
            var json = JsonSerializer.Serialize(evt.Payload, AutonogrammerJsonContext.Default.StreamEventPayload);
            yield return new SseItem<string>(json, StreamEventKind.Of(evt.Payload))
            {
                EventId = evt.Seq.ToString(CultureInfo.InvariantCulture),
                ReconnectionInterval = TimeSpan.FromSeconds(15),
            };

            if (StreamEventKind.IsTerminal(evt.Payload))
            {
                yield break;
            }
        }
    }

    private static async Task<IResult> ListSecretsAsync(ICloudSecretStore secrets, IUserContext user, CancellationToken cancellationToken) =>
        TypedResults.Ok(new CloudSecretListResponse { Secrets = await secrets.ListAsync(user.Name, cancellationToken).ConfigureAwait(false) });

    private static async Task<IResult> GetSecretAsync(string name, ICloudSecretStore secrets, IUserContext user, CancellationToken cancellationToken)
    {
        var info = CloudSecretNames.IsValid(name) ? await secrets.GetInfoAsync(user.Name, name, cancellationToken).ConfigureAwait(false) : null;
        return info is null ? Problem(SecretNotFound()) : TypedResults.Ok(info);
    }

    private static async Task<IResult> PutSecretAsync(
        HttpContext context,
        string name,
        ICloudSecretStore secrets,
        IUserContext user,
        IOptions<ApiOptions> apiOptions,
        IOptions<CloudOptions> cloudOptions,
        CancellationToken cancellationToken)
    {
        if (!CloudSecretNames.IsValid(name))
        {
            return Problem(CloudProblem.BadRequest("invalid_secret_name", "Invalid secret name.", "A secret name is 1 to 64 letters, digits and underscores, does not start with a digit, and is not reserved."));
        }

        var body = await WebBodyReader.ReadAsync(context, apiOptions.Value, CloudJsonContext.Default.CloudSecretValueRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        if (!CloudSecretNames.IsValidValue(body.Value!.Value))
        {
            return Problem(CloudProblem.BadRequest("invalid_secret_value", "Invalid secret value.", "A secret value is 4 to 8192 characters without control characters other than line breaks and tabs."));
        }

        var info = await secrets.SetAsync(user.Name, name, body.Value.Value!, cloudOptions.Value.Limits.MaxSecretsPerOwner, cancellationToken).ConfigureAwait(false);
        return info is null
            ? Problem(new CloudProblem(429, "secret_limit_reached", "Too many secrets.", "This caller reached the maximum number of secrets. Delete one first."))
            : TypedResults.Ok(info);
    }

    private static async Task<IResult> DeleteSecretAsync(string name, ICloudSecretStore secrets, IUserContext user, CancellationToken cancellationToken)
    {
        var deleted = CloudSecretNames.IsValid(name) && await secrets.DeleteAsync(user.Name, name, cancellationToken).ConfigureAwait(false);
        return deleted ? TypedResults.NoContent() : Problem(SecretNotFound());
    }

    private static async Task<IResult> LimitsAsync(CloudAgentService service, IUserContext user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.LimitsAsync(user.Name, cancellationToken).ConfigureAwait(false));

    private static CloudProblem SecretNotFound() => new(404, "secret_not_found", "Secret not found.", "The caller has no secret of that name.");

    private static IResult Respond(CloudOutcome<CloudAgentResponse> outcome) =>
        outcome.Problem is not null ? Problem(outcome.Problem) : TypedResults.Ok(outcome.Value);

    private static ProblemHttpResult Problem(CloudProblem problem) => WebProblems.Of(problem.Status, problem.Code, problem.Title, problem.Detail);
}
