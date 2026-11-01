#pragma warning disable MA0048 // The plan owns this file for the routes and the account store.
using System.Globalization;
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
        MapRoute(endpoints.MapGet("/cowork/projects", List), "ListCoworkProjects", "Lists the caller's projects.");
        MapRoute(endpoints.MapGet("/cowork/projects/{id}", Get), "GetCoworkProject", "Reads one project the caller owns. Another account's id is 404.");
        MapRoute(endpoints.MapPut("/cowork/projects/{id}", UpsertAsync), "PutCoworkProject", "Creates or replaces one project owned by the caller.");
        MapRoute(endpoints.MapPost("/cowork/projects/{id}/archive", ArchiveAsync), "ArchiveCoworkProject", "Archives one project the caller owns.");
        MapRoute(endpoints.MapDelete("/cowork/projects/{id}", Delete), "DeleteCoworkProject", "Deletes one project the caller owns.");
        MapRoute(endpoints.MapGet("/cowork/instructions/global", GetGlobal), "GetCoworkGlobalInstructions", "Reads the caller's global instructions.");
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

    private static IResult List(ProjectAccountStore store, IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var json = JsonSerializer.Serialize(store.List(user.Name).ToList(), ProjectsJsonContext.Default.ListProjectSyncRecord);
        return Results.Text(json, "application/json");
    }

    private static IResult Get(string id, ProjectAccountStore store, IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var project = store.Get(user.Name, id);
        return project is null ? Missing() : Json(project);
    }

    private static async Task<IResult> UpsertAsync(
        HttpContext context,
        string id,
        ProjectAccountStore store,
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

        var errors = Validate(request, store.Count(user.Name, id));
        if (errors.Count > 0)
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "project_invalid", "Project is invalid.", errors[0]);
        }

        var (record, created) = store.Upsert(user.Name, request, time.GetUtcNow());
        var status = created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        var body = JsonSerializer.Serialize(record, ProjectsJsonContext.Default.ProjectSyncRecord);
        if (key is not null)
        {
            idempotency.Remember(IdempotencyScope, user.Name, key, status, body);
        }

        return Results.Text(body, "application/json", statusCode: status);
    }

    private static async Task<IResult> ArchiveAsync(
        HttpContext context,
        string id,
        ProjectAccountStore store,
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

        var archived = store.Archive(user.Name, id, time.GetUtcNow());
        return archived is null ? Missing() : Json(archived);
    }

    private static IResult Delete(string id, ProjectAccountStore store, IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        return store.Delete(user.Name, id) ? Results.NoContent() : Missing();
    }

    private static IResult GetGlobal(ProjectAccountStore store, IUserContext user)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var json = JsonSerializer.Serialize(new GlobalInstructionsRecord { Instructions = store.GetInstructions(user.Name) }, ProjectsJsonContext.Default.GlobalInstructionsRecord);
        return Results.Text(json, "application/json");
    }

    private static async Task<IResult> PutGlobalAsync(HttpContext context, ProjectAccountStore store, IUserContext user, CancellationToken cancellationToken)
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

        store.SetInstructions(user.Name, instructions);
        var json = JsonSerializer.Serialize(new GlobalInstructionsRecord { Instructions = instructions }, ProjectsJsonContext.Default.GlobalInstructionsRecord);
        return Results.Text(json, "application/json");
    }

    private static List<string> Validate(ProjectSyncRecord project, int existingCount)
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

        if (existingCount >= 200)
        {
            errors.Add(string.Create(CultureInfo.InvariantCulture, $"At most 200 projects per account."));
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

/// <summary>In-process project rows keyed by the token subject. Another account's row is absent.</summary>
internal sealed class ProjectAccountStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Dictionary<string, ProjectSyncRecord>> _projects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _instructions = new(StringComparer.Ordinal);

    /// <summary>Lists one account's projects.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <returns>The projects.</returns>
    public IReadOnlyList<ProjectSyncRecord> List(string accountId)
    {
        lock (_gate)
        {
            return Rows(accountId).Values.ToArray();
        }
    }

    /// <summary>Reads one project when the account owns it.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <param name="id">The project id.</param>
    /// <returns>The project, or null.</returns>
    public ProjectSyncRecord? Get(string accountId, string id)
    {
        lock (_gate)
        {
            return Rows(accountId).TryGetValue(id, out var project) ? project : null;
        }
    }

    /// <summary>Returns how many projects the account holds, ignoring an id that already exists.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <param name="id">The id being written.</param>
    /// <returns>The count used for the 200 cap.</returns>
    public int Count(string accountId, string id)
    {
        lock (_gate)
        {
            var rows = Rows(accountId);
            return rows.ContainsKey(id) ? rows.Count - 1 : rows.Count;
        }
    }

    /// <summary>Creates or replaces one project. Archived time already stored is kept unless this write sets one.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <param name="project">The project.</param>
    /// <param name="now">The update time.</param>
    /// <returns>The stored project and whether it was created.</returns>
    public (ProjectSyncRecord Record, bool Created) Upsert(string accountId, ProjectSyncRecord project, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            var rows = Rows(accountId);
            var created = !rows.TryGetValue(project.Id, out var existing);
            var stored = project with
            {
                UpdatedAt = now,
                ArchivedAt = project.ArchivedAt ?? existing?.ArchivedAt,
            };
            rows[project.Id] = stored;
            return (stored, created);
        }
    }

    /// <summary>Archives one owned project.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <param name="id">The project id.</param>
    /// <param name="now">The archive time.</param>
    /// <returns>The archived project, or null.</returns>
    public ProjectSyncRecord? Archive(string accountId, string id, DateTimeOffset now)
    {
        lock (_gate)
        {
            var rows = Rows(accountId);
            if (!rows.TryGetValue(id, out var existing))
            {
                return null;
            }

            var archived = existing with { ArchivedAt = now, UpdatedAt = now };
            rows[id] = archived;
            return archived;
        }
    }

    /// <summary>Deletes one owned project.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <param name="id">The project id.</param>
    /// <returns>True when a row was removed.</returns>
    public bool Delete(string accountId, string id)
    {
        lock (_gate)
        {
            return Rows(accountId).Remove(id);
        }
    }

    /// <summary>Reads global instructions. A missing document is empty.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <returns>The instructions.</returns>
    public string GetInstructions(string accountId)
    {
        lock (_gate)
        {
            return _instructions.TryGetValue(accountId, out var text) ? text : string.Empty;
        }
    }

    /// <summary>Replaces global instructions.</summary>
    /// <param name="accountId">The token subject.</param>
    /// <param name="instructions">The text.</param>
    public void SetInstructions(string accountId, string instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        lock (_gate)
        {
            _instructions[accountId] = instructions;
        }
    }

    private Dictionary<string, ProjectSyncRecord> Rows(string accountId)
    {
        if (!_projects.TryGetValue(accountId, out var rows))
        {
            rows = new Dictionary<string, ProjectSyncRecord>(StringComparer.Ordinal);
            _projects[accountId] = rows;
        }

        return rows;
    }
}
