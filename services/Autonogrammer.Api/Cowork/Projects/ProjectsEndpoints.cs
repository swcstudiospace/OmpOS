#pragma warning disable MA0048 // The plan owns this file for the routes and the account store.
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.Idempotency;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Cowork.Projects;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Autonogrammer.Api.Cowork.Projects;

/// <summary>Per-account project and global-instruction routes. The owner is always the token subject.</summary>
internal static class ProjectsEndpoints
{
    private const string IdempotencyScope = "projects";

    /// <summary>Maps the project routes.</summary>
    /// <param name="endpoints">The route builder.</param>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        MapRoute(endpoints.MapGet("/cowork/projects", ListAsync), "ListCoworkProjects", "Lists the caller's projects.");
        MapRoute(endpoints.MapGet("/cowork/projects/{id}", GetAsync), "GetCoworkProject", "Reads one project the caller owns. Another account's id is 404.");
        MapRoute(endpoints.MapPut("/cowork/projects/{id}", UpsertAsync), "PutCoworkProject", "Creates or replaces one project owned by the caller.");
        MapRoute(endpoints.MapPost("/cowork/projects/{id}/archive", ArchiveAsync), "ArchiveCoworkProject", "Archives one project the caller owns.");
        MapRoute(endpoints.MapDelete("/cowork/projects/{id}", DeleteAsync), "DeleteCoworkProject", "Deletes one project the caller owns.");
        MapRoute(endpoints.MapGet("/cowork/instructions/global", GetGlobalAsync), "GetCoworkGlobalInstructions", "Reads the caller's global instructions.");
        MapRoute(endpoints.MapPut("/cowork/instructions/global", PutGlobalAsync), "PutCoworkGlobalInstructions", "Replaces the caller's global instructions.");
    }

    private static void MapRoute(RouteHandlerBuilder route, string name, string summary) =>
        route.WithName(name)
            .WithTags("Cowork projects")
            .WithSummary(summary)
            .WithMetadata(CoworkProjectsRouteMarker.Instance)
            .RequireAuthorization()
            .RequireRateLimiting(CoworkProjectsRateLimiterPolicy.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> ListAsync(PostgresProjectAccountStore store, IUserContext user, CancellationToken cancellationToken)
    {
        var projects = await store.ListAsync(user.Name, cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(projects.ToList(), ProjectsJsonContext.Default.ListProjectSyncRecord);
        return Results.Text(json, "application/json");
    }

    private static async Task<IResult> GetAsync(string id, PostgresProjectAccountStore store, IUserContext user, CancellationToken cancellationToken)
    {
        var project = await store.GetAsync(user.Name, id, cancellationToken).ConfigureAwait(false);
        return project is null ? Missing() : Json(project);
    }

    private static async Task<IResult> UpsertAsync(
        HttpContext context,
        string id,
        PostgresProjectAccountStore store,
        CoworkIdempotencyCache idempotency,
        IUserContext user,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(time);
        if (!IdempotencyKeyHeader.TryRead(context.Request.Headers, out var key))
        {
            return InvalidKey();
        }

        if (key is not null && idempotency.TryGet(IdempotencyScope, user.Name, key, out var replay))
        {
            return Results.Text(replay.Body, "application/json", statusCode: replay.StatusCode);
        }

        var read = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        ProjectSyncRecord? request;
        try
        {
            request = JsonSerializer.Deserialize(read.Bytes, ProjectsJsonContext.Default.ProjectSyncRecord);
        }
        catch (JsonException)
        {
            return InvalidJson();
        }

        if (request is null || !string.Equals(request.Id, id, StringComparison.Ordinal))
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "project_id_mismatch", "Project id mismatch.", "The project id in the body must match the route.");
        }
        if (request.ContextUrls is null)
        {
            request = request with { ContextUrls = [] };
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "project_invalid", "Project is invalid.", errors[0]);
        }

        var (record, created, capped) = await store.UpsertAsync(user.Name, request, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return UpsertResult(record, created, capped, idempotency, user.Name, key);
    }

    private static IResult UpsertResult(ProjectSyncRecord? record, bool created, bool capped, CoworkIdempotencyCache idempotency, string account, string? key)
    {
        if (capped)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "project_invalid", "Project is invalid.", "At most 200 projects per account.");
        }

        if (record is null)
        {
            return Missing();
        }
        var status = created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        var body = JsonSerializer.Serialize(record, ProjectsJsonContext.Default.ProjectSyncRecord);
        if (key is not null)
        {
            idempotency.Remember(IdempotencyScope, account, key, status, body);
        }

        return Results.Text(body, "application/json", statusCode: status);
    }

    private static async Task<IResult> ArchiveAsync(
        HttpContext context,
        string id,
        PostgresProjectAccountStore store,
        IUserContext user,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(time);
        var read = await ReadOptionalAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var archived = await store.ArchiveAsync(user.Name, id, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return archived is null ? Missing() : Json(archived);
    }

    private static async Task<IResult> DeleteAsync(string id, PostgresProjectAccountStore store, IUserContext user, CancellationToken cancellationToken)
    {
        return await store.DeleteAsync(user.Name, id, cancellationToken).ConfigureAwait(false) ? Results.NoContent() : Missing();
    }

    private static async Task<IResult> GetGlobalAsync(PostgresProjectAccountStore store, IUserContext user, CancellationToken cancellationToken)
    {
        var instructions = await store.GetInstructionsAsync(user.Name, cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(new GlobalInstructionsRecord { Instructions = instructions }, ProjectsJsonContext.Default.GlobalInstructionsRecord);
        return Results.Text(json, "application/json");
    }

    private static async Task<IResult> PutGlobalAsync(HttpContext context, PostgresProjectAccountStore store, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var read = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        GlobalInstructionsRecord? request;
        try
        {
            request = JsonSerializer.Deserialize(read.Bytes, ProjectsJsonContext.Default.GlobalInstructionsRecord);
        }
        catch (JsonException)
        {
            return InvalidJson();
        }

        var instructions = request?.Instructions ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(instructions) > 8 * 1024)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "instructions_too_large", "Instructions too large.", "Global instructions must be at most 8 KiB.");
        }

        await store.SetInstructionsAsync(user.Name, instructions, cancellationToken).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(new GlobalInstructionsRecord { Instructions = instructions }, ProjectsJsonContext.Default.GlobalInstructionsRecord);
        return Results.Text(json, "application/json");
    }

    private static List<string> Validate(ProjectSyncRecord project)
    {
        var errors = new List<string>();
        var nameLength = project.Name?.Length ?? 0;
        if (string.IsNullOrWhiteSpace(project.Name) || nameLength is < 1 or > 100)
        {
            errors.Add("Name must be 1 to 100 characters.");
        }

        if (Utf8(project.Description) > 16 * 1024 || Utf8(project.Instructions) > 16 * 1024)
        {
            errors.Add("Instructions must be at most 16 KiB.");
        }

        var urls = project.ContextUrls ?? [];
        if (urls.Count > 20)
        {
            errors.Add("Context URLs must be at most 20 https links.");
        }

        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                errors.Add("Context URLs must be https links.");
                break;
            }
        }


        return errors;
    }

    private static int Utf8(string? value) => value is null ? 0 : Encoding.UTF8.GetByteCount(value);

    private static IResult Json(ProjectSyncRecord project) =>
        Results.Text(JsonSerializer.Serialize(project, ProjectsJsonContext.Default.ProjectSyncRecord), "application/json");

    private static ProblemHttpResult Missing() =>
        WebProblems.Of(StatusCodes.Status404NotFound, "project_not_found", "Project not found.", "That project was not found.");

    private static ProblemHttpResult InvalidJson() =>
        WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_json", "Invalid request body.", "The request body is not valid JSON.");

    private static ProblemHttpResult InvalidKey() =>
        WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_idempotency_key", "Invalid idempotency key.", "The Idempotency-Key header must appear at most once and hold 1 to 128 visible ASCII characters.");

    private static async Task<BodyRead> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var read = await ReadOptionalAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null || read.Bytes.Length > 0)
        {
            return read;
        }

        return new BodyRead([], WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_json", "Invalid request body.", "The request body is required."));
    }

    private static async Task<BodyRead> ReadOptionalAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.ContentLength is 0)
        {
            return new BodyRead([], null);
        }

        if (context.Request.ContentType is not null && !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return new BodyRead([], WebProblems.Of(StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type", "Unsupported media type.", "Content-Type must be application/json."));
        }

        using var buffer = new MemoryStream();
        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            int read;
            while ((read = await context.Request.Body.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > CoworkRequestBodies.MaxBodyBytes)
                {
                    return new BodyRead([], WebProblems.Of(StatusCodes.Status413PayloadTooLarge, "payload_too_large", "Request body too large.", "The request body exceeds the configured limit."));
                }

                await buffer.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }

        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            return new BodyRead(bytes, null);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (CoworkRequestBodies.NamesAccount(document.RootElement))
            {
                return new BodyRead(bytes, WebProblems.Of(StatusCodes.Status400BadRequest, "owner_not_accepted", "Owner not accepted.", "The owner is the authenticated caller. The request body must not name an account."));
            }

            if (CoworkRequestBodies.NamesFolder(document.RootElement))
            {
                return new BodyRead(bytes, WebProblems.Of(StatusCodes.Status400BadRequest, "folders_not_accepted", "Folders not accepted.", "Folders stay on the device. The request body must not name a folder."));
            }
        }
        catch (JsonException)
        {
            return new BodyRead(bytes, InvalidJson());
        }

        return new BodyRead(bytes, null);
    }

    private readonly record struct BodyRead(byte[] Bytes, IResult? Error);
}

