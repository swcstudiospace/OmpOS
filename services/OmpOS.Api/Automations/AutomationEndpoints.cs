using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Api.Security;
using OmpOS.Api.Services;
using OmpOS.Contracts.Automations;
using OmpOS.Harness.Automations.Cron;
using OmpOS.Harness.Automations.Hosted;

namespace OmpOS.Api.Automations;

/// <summary>
/// The automation routes. They are always mapped (the OpenAPI document describes them); <see cref="AutomationDisabledMiddleware"/> answers 404
/// <c>automations_disabled</c> for every one of them, including the webhook receiver, while <c>Automations:Enabled</c> is false. The management routes are owner-scoped: an automation of another owner reads as absent. The
/// receiver <c>POST /hooks/automations/{id}</c> is anonymous and authenticates by signature; every authentication failure answers the
/// same 401, whichever check failed. Problems carry a stable code in <c>type</c> (<c>/problems/{code}</c>). The list of full definitions is
/// <c>GET /automations/definitions</c> because <c>GET /automations</c> belongs to the settings summary.
/// </summary>
internal static class AutomationEndpoints
{
    private const int MaxPreviewCount = 20;

    /// <summary>Maps the automation routes, once per host. A second call maps nothing.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!endpoints.ServiceProvider.GetRequiredService<AutomationEndpointsMapped>().TryMark())
        {
            return endpoints;
        }

        var routes = endpoints.MapGroup(string.Empty).WithMetadata(AutomationRouteMarker.Instance);
        MapCollectionRoutes(routes);
        MapItemRoutes(routes);
        MapWebhookReceiver(routes);
        return endpoints;
    }

    private static void MapCollectionRoutes(RouteGroupBuilder routes)
    {
        routes.MapPost("/automations", CreateAsync)
            .WithTags("Automations")
            .WithSummary("Creates an automation: a prompt, triggers (cron, webhook) and a target. A webhook secret is generated and shown once in the answer.")
            .Accepts<SaveAutomationRequest>("application/json")
            .Produces<AutomationSaveResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapGet("/automations/definitions", ListAsync)
            .WithTags("Automations")
            .WithSummary("Lists the caller's automations with their next run and last run, newest first.")
            .Produces<AutomationListResponse>()
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapGet("/automations/cron-preview", CronPreview)
            .WithTags("Automations")
            .WithSummary("Shows the next occurrences of a cron expression in a time zone.")
            .Produces<AutomationCronPreviewResponse>()
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapGet("/automations/{id}", GetAsync)
            .WithTags("Automations")
            .WithSummary("Reads one automation.")
            .Produces<AutomationInfo>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
    }

    private static void MapItemRoutes(RouteGroupBuilder routes)
    {
        routes.MapPut("/automations/{id}", ReplaceAsync)
            .WithTags("Automations")
            .WithSummary("Replaces an automation's definition. The webhook secret is kept; one is generated when the first webhook trigger is added.")
            .Accepts<SaveAutomationRequest>("application/json")
            .Produces<AutomationSaveResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapDelete("/automations/{id}", DeleteAsync)
            .WithTags("Automations")
            .WithSummary("Deletes an automation with its history and its webhook secret.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapPut("/automations/{id}/enabled", SetEnabledAsync)
            .WithTags("Automations")
            .WithSummary("Turns an automation's triggers on or off. A run by hand stays possible while it is off.")
            .Accepts<SetAutomationEnabledRequest>("application/json")
            .Produces<AutomationInfo>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapPost("/automations/{id}/webhook-secret", RotateSecretAsync)
            .WithTags("Automations")
            .WithSummary("Replaces the webhook secret. The new secret is shown once; the old one stops working at once.")
            .Produces<AutomationSaveResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapPost("/automations/{id}/runs", RunNowAsync)
            .WithTags("Automations")
            .WithSummary("Runs an automation now, whether or not it is enabled. The run continues in the background.")
            .Produces<AutomationRunInfo>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
        routes.MapGet("/automations/{id}/runs", RunsAsync)
            .WithTags("Automations")
            .WithSummary("Reads an automation's run history, newest first.")
            .Produces<AutomationRunListResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireRateLimiting(AutomationApiRateLimiterPolicy.PolicyName);
    }

    private static void MapWebhookReceiver(RouteGroupBuilder routes)
    {
        routes.MapPost("/hooks/automations/{id}", ReceiveAsync)
            .WithTags("Automations")
            .WithSummary("The webhook receiver. Anonymous, authenticated by HMAC-SHA256 signature; every authentication failure answers the same 401.")
            .AllowAnonymous()
            .Produces<AutomationWebhookResponse>(StatusCodes.Status202Accepted)
            .Produces<AutomationWebhookResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(AutomationWebhookRateLimiterPolicy.PolicyName);
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        AutomationService service,
        AutomationRunner runner,
        IAutomationStore store,
        IUserContext user,
        TimeProvider clock,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        var body = await WebBodyReader.ReadAsync(context, options.Value, AutomationsJsonContext.Default.SaveAutomationRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var prepared = Prepare(body.Value!, runner);
        if (prepared.Issues.Count > 0)
        {
            return Invalid(prepared.Issues);
        }

        var change = await service.CreateAsync(user.Name, prepared.Draft, cancellationToken).ConfigureAwait(false);
        if (!change.Succeeded)
        {
            return Invalid(change.Issues);
        }

        var response = await SavedAsync(change, service, store, clock, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/automations/{change.Automation!.Id}", response);
    }

    private static async Task<IResult> ListAsync(
        AutomationService service,
        IAutomationStore store,
        IUserContext user,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var automations = new List<AutomationInfo>();
        foreach (var automation in await service.ListAsync(user.Name, cancellationToken).ConfigureAwait(false))
        {
            automations.Add(await InfoAsync(automation, service, store, clock, cancellationToken).ConfigureAwait(false));
        }

        return TypedResults.Ok(new AutomationListResponse { Automations = automations });
    }

    private static Ok<AutomationCronPreviewResponse> CronPreview(string? expression, string? timeZone, int? count, TimeProvider clock)
    {
        if (!CronExpression.TryParse(expression, out var parsed, out var error))
        {
            return TypedResults.Ok(new AutomationCronPreviewResponse { NextRuns = [], Error = error });
        }

        if (!AutomationTimeZones.TryResolve(string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone, out var zone))
        {
            return TypedResults.Ok(new AutomationCronPreviewResponse { NextRuns = [], Error = "The time zone is not known to this host. Use an IANA name such as Europe/Berlin, or UTC." });
        }

        var wanted = Math.Clamp(count ?? 5, 1, MaxPreviewCount);
        return TypedResults.Ok(new AutomationCronPreviewResponse { NextRuns = parsed.GetNextOccurrences(clock.GetUtcNow(), zone, wanted) });
    }

    private static async Task<IResult> GetAsync(
        string id,
        AutomationService service,
        IAutomationStore store,
        IUserContext user,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var automation = await service.GetAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        return automation is null
            ? NotFound()
            : TypedResults.Ok(await InfoAsync(automation, service, store, clock, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> ReplaceAsync(
        HttpContext context,
        string id,
        AutomationService service,
        AutomationRunner runner,
        IAutomationStore store,
        IUserContext user,
        TimeProvider clock,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        var body = await WebBodyReader.ReadAsync(context, options.Value, AutomationsJsonContext.Default.SaveAutomationRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var prepared = Prepare(body.Value!, runner);
        if (prepared.Issues.Count > 0)
        {
            return Invalid(prepared.Issues);
        }

        var change = await service.ReplaceAsync(user.Name, id, prepared.Draft, cancellationToken).ConfigureAwait(false);
        if (change.NotFound)
        {
            return NotFound();
        }

        return change.Succeeded
            ? TypedResults.Ok(await SavedAsync(change, service, store, clock, cancellationToken).ConfigureAwait(false))
            : Invalid(change.Issues);
    }

    private static async Task<IResult> DeleteAsync(string id, AutomationService service, IUserContext user, CancellationToken cancellationToken) =>
        await service.DeleteAsync(user.Name, id, cancellationToken).ConfigureAwait(false) ? TypedResults.NoContent() : NotFound();

    private static async Task<IResult> SetEnabledAsync(
        HttpContext context,
        string id,
        AutomationService service,
        IAutomationStore store,
        IUserContext user,
        TimeProvider clock,
        IOptions<ApiOptions> options,
        CancellationToken cancellationToken)
    {
        var body = await WebBodyReader.ReadAsync(context, options.Value, AutomationsJsonContext.Default.SetAutomationEnabledRequest, cancellationToken).ConfigureAwait(false);
        if (body.Error is not null)
        {
            return body.Error;
        }

        var change = await service.SetEnabledAsync(user.Name, id, body.Value!.Enabled, cancellationToken).ConfigureAwait(false);
        return change.Succeeded
            ? TypedResults.Ok(await InfoAsync(change.Automation!, service, store, clock, cancellationToken).ConfigureAwait(false))
            : NotFound();
    }

    private static async Task<IResult> RotateSecretAsync(
        string id,
        AutomationService service,
        IAutomationStore store,
        IUserContext user,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var change = await service.RotateWebhookSecretAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        if (change.NotFound)
        {
            return NotFound();
        }

        return change.Succeeded
            ? TypedResults.Ok(await SavedAsync(change, service, store, clock, cancellationToken).ConfigureAwait(false))
            : Invalid(change.Issues);
    }

    private static async Task<IResult> RunNowAsync(
        string id,
        AutomationService service,
        AutomationRunner runner,
        IUserContext user,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var automation = await service.GetAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        if (automation is null)
        {
            return NotFound();
        }

        var start = await runner.StartAsync(automation, AutomationTriggerContext.Manual(), cancellationToken).ConfigureAwait(false);
        if (!start.Admitted)
        {
            return WebProblems.Of(
                StatusCodes.Status409Conflict,
                start.Refusal == AutomationRunRefusal.HourlyCap ? "automation_hourly_cap" : "automation_busy",
                "The automation did not start.",
                start.Refusal == AutomationRunRefusal.HourlyCap
                    ? "The automation reached its runs-per-hour cap. Try again later or raise the cap."
                    : "The automation already has its maximum number of runs in progress.");
        }

        return TypedResults.Accepted(
            $"/automations/{id}/runs",
            new AutomationRunInfo
            {
                RunId = start.RunId!,
                Trigger = "manual",
                Summary = "Run now",
                Status = "running",
                StartedAt = clock.GetUtcNow(),
            });
    }

    private static async Task<IResult> RunsAsync(string id, AutomationService service, IAutomationStore store, IUserContext user, CancellationToken cancellationToken)
    {
        if (await service.GetAsync(user.Name, id, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound();
        }

        var history = await store.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(new AutomationRunListResponse { Runs = history.Select(AutomationMapper.ToRunInfo).ToArray() });
    }

    private static async Task<IResult> ReceiveAsync(
        HttpContext context,
        string id,
        AutomationWebhookReceiver receiver,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger("OmpOS.Api.Automations.Webhook");
        var limit = receiver.MaxBodyBytes;
        if (context.Request.ContentLength is { } declared && declared > limit)
        {
            return TooLarge();
        }

        var bytes = await ReadBoundedAsync(context.Request.Body, limit, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return TooLarge();
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in context.Request.Headers)
        {
            headers[name] = values.ToString();
        }

        var receipt = await receiver.HandleAsync(id, new WebhookRequest(bytes, headers), cancellationToken).ConfigureAwait(false);
        switch (receipt.Outcome)
        {
            case WebhookOutcome.Accepted when receipt.Refusal is null:
                AutomationLog.WebhookAccepted(logger, id, receipt.RunId!);
                return TypedResults.Accepted(
                    (string?)null,
                    new AutomationWebhookResponse { Status = "accepted", RunId = receipt.RunId });
            case WebhookOutcome.Accepted:
                AutomationLog.WebhookRunRefused(logger, id, receipt.Refusal?.ToString() ?? string.Empty);
                return WebProblems.Of(StatusCodes.Status429TooManyRequests, "automation_limit", "The automation did not start.", "The automation reached its concurrency limit or its runs-per-hour cap.");
            case WebhookOutcome.Ping:
                return TypedResults.Ok(new AutomationWebhookResponse { Status = "pong" });
            case WebhookOutcome.Ignored:
                return TypedResults.Ok(new AutomationWebhookResponse { Status = "ignored" });
            case WebhookOutcome.Replay:
                AutomationLog.WebhookRejected(logger, id, receipt.Reason);
                return WebProblems.Of(StatusCodes.Status409Conflict, "webhook_replayed", "Delivery already received.", "This delivery was already accepted.");
            case WebhookOutcome.TooLarge:
                return TooLarge();
            default:
                AutomationLog.WebhookRejected(logger, id, receipt.Reason);
                return Unauthorized();
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(Stream body, int limit, CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (destination.Length + read > limit)
            {
                return null;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return destination.ToArray();
    }

    private static (AutomationDraft Draft, List<AutomationValidationIssue> Issues) Prepare(SaveAutomationRequest request, AutomationRunner runner)
    {
        var issues = new List<AutomationValidationIssue>();
        var draft = AutomationMapper.ToDraft(request, issues);
        if (issues.Count == 0 && !runner.HasTarget(draft.Target.Kind))
        {
            issues.Add(new AutomationValidationIssue("target", "This API cannot run local automations. Use a cloud target, or run the automation from the desktop app."));
        }

        return (draft, issues);
    }

    private static async Task<AutomationSaveResponse> SavedAsync(AutomationChange change, AutomationService service, IAutomationStore store, TimeProvider clock, CancellationToken cancellationToken) =>
        new()
        {
            Automation = await InfoAsync(change.Automation!, service, store, clock, cancellationToken).ConfigureAwait(false),
            WebhookSecret = change.WebhookSecret,
        };

    private static async Task<AutomationInfo> InfoAsync(UserAutomation automation, AutomationService service, IAutomationStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        var history = await store.GetHistoryAsync(automation.Id, cancellationToken).ConfigureAwait(false);
        var hasSecret = automation.HasWebhook && await service.HasWebhookSecretAsync(automation, cancellationToken).ConfigureAwait(false);
        return AutomationMapper.ToInfo(automation, history.Count == 0 ? null : history[0], hasSecret, clock.GetUtcNow());
    }

    private static ProblemHttpResult Invalid(IReadOnlyList<AutomationValidationIssue> issues) =>
        WebProblems.Of(
            StatusCodes.Status400BadRequest,
            "invalid_automation",
            "Invalid automation.",
            string.Join('\n', issues.Select(static issue => string.Create(CultureInfo.InvariantCulture, $"{issue.Field}: {issue.Message}"))));

    private static ProblemHttpResult NotFound() =>
        WebProblems.Of(StatusCodes.Status404NotFound, "automation_not_found", "Automation not found.", "No automation of yours has that id.");

    private static ProblemHttpResult TooLarge() =>
        WebProblems.Of(StatusCodes.Status413PayloadTooLarge, "webhook_too_large", "Request body too large.", "The request body exceeds the webhook size limit of 256 KiB.");

    private static ProblemHttpResult Unauthorized() =>
        WebProblems.Of(StatusCodes.Status401Unauthorized, "webhook_unauthorized", "Webhook not authorized.", "The delivery could not be authenticated.");
}
