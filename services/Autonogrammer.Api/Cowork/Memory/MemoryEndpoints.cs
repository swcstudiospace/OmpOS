#pragma warning disable MA0048 // The plan owns this file for the routes and the body reader.
using System.Text.Json;
using Autonogrammer.Api.Idempotency;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Cowork.Memory;
using Autonogrammer.Cowork.Memory;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork.Memory;

/// <summary>Per-account memory routes. The owner is always the token subject. Caps match the desktop store.</summary>
internal static class MemoryEndpoints
{
    /// <summary>Maps the memory routes.</summary>
    /// <param name="endpoints">The route builder.</param>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        MapRoute(endpoints.MapPost("/cowork/memory", SaveAsync), "CreateCoworkMemory", "Saves one memory entry for the caller.");
        MapRoute(endpoints.MapGet("/cowork/memory", ListAsync), "ListCoworkMemory", "Lists the caller's newest account entries.");
        MapRoute(endpoints.MapPost("/cowork/memory/recall", RecallAsync), "RecallCoworkMemory", "Searches the caller's memory with the store's plain text query.");
        MapRoute(endpoints.MapPost("/cowork/memory/reset", ResetAsync), "ResetCoworkMemory", "Hard-deletes every memory entry for the caller, including project memory.");
        MapRoute(endpoints.MapGet("/cowork/memory/export", ExportAsync), "ExportCoworkMemory", "Exports the caller's memory, including project memory, as JSON.");
        MapRoute(endpoints.MapGet("/cowork/memory/settings", GetSettingsAsync), "GetCoworkMemorySettings", "Reads the caller's memory settings.");
        MapRoute(endpoints.MapPut("/cowork/memory/settings", PutSettingsAsync), "PutCoworkMemorySettings", "Replaces the caller's memory settings.");
        MapRoute(endpoints.MapGet("/cowork/memory/{id}", GetAsync), "GetCoworkMemory", "Reads one memory entry the caller owns. Another account's id is 404.");
        MapRoute(endpoints.MapDelete("/cowork/memory/{id}", DeleteAsync), "DeleteCoworkMemory", "Deletes one memory entry the caller owns.");
    }

    private static void MapRoute(RouteHandlerBuilder route, string name, string summary) =>
        route.WithName(name)
            .WithTags("Cowork memory")
            .WithSummary(summary)
            .WithMetadata(CoworkMemoryRouteMarker.Instance)
            .RequireAuthorization()
            .RequireRateLimiting(CoworkMemoryRateLimiterPolicy.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<IResult> SaveAsync(
        HttpContext context,
        IMemoryStore store,
        IOptions<MemoryOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(user);
        if (!IdempotencyKeyHeader.TryRead(context.Request.Headers, out var key))
        {
            return InvalidKey();
        }

        var read = await CoworkBodies.ReadRequiredAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        MemorySaveRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(read.Bytes, MemoryJsonContext.Default.MemorySaveRequest);
        }
        catch (JsonException)
        {
            return InvalidJson();
        }

        if (request is null || string.IsNullOrEmpty(request.Content))
        {
            return WebProblems.Of(StatusCodes.Status400BadRequest, "memory_invalid", "Memory is invalid.", "Content is required.");
        }

        if (options.Value.Paused)
        {
            return Paused();
        }
        var scope = new MemoryScope(user.Name, request.ProjectId, request.UseAccountMemory, request.SourceSession ?? "api");

        var draft = new MemoryDraft(request.Topic, request.Content, request.Category);
        if (key is not null)
        {
            return await SaveKeyedAsync(context, store, scope, draft, key, cancellationToken).ConfigureAwait(false);
        }

        var mutation = await store.SaveAsync(scope, draft, cancellationToken).ConfigureAwait(false);
        return Saved(mutation);
    }

    private static async Task<IResult> SaveKeyedAsync(
        HttpContext context, IMemoryStore store, MemoryScope scope, MemoryDraft draft, string key, CancellationToken cancellationToken)
    {
        if (store is not IMemoryIdempotentStore durable
            || (store is InMemoryMemoryStore && context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction()))
        {
            return Unavailable();
        }

        var result = await durable.SaveIdempotentAsync(scope, draft, key, cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            MemoryIdempotentStatus.Conflict => WebProblems.Of(StatusCodes.Status409Conflict, "idempotency_conflict", "Idempotency conflict.", "The key was already used for a different request."),
            MemoryIdempotentStatus.Gone => Results.StatusCode(StatusCodes.Status410Gone),
            MemoryIdempotentStatus.Refused => result.Mutation is null ? Unavailable() : SaveFailure(result.Mutation),
            MemoryIdempotentStatus.Created or MemoryIdempotentStatus.Replayed => result.Mutation is null ? Unavailable() : Saved(result.Mutation),
            _ => Unavailable(),
        };
    }

    private static IResult Saved(MemoryMutation mutation)
    {
        if (!mutation.Succeeded || mutation.Entry is null)
        {
            return SaveFailure(mutation);
        }

        var record = MemorySearch.ToRecord(mutation.Entry);
        var body = JsonSerializer.Serialize(record, MemoryJsonContext.Default.MemoryEntryRecord);
        return Results.Text(body, "application/json", statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListAsync(IMemoryStore store, IOptions<MemoryOptions> options, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(user);
        if (options.Value.Paused || (await store.GetSettingsAsync(user.Name, cancellationToken).ConfigureAwait(false)).Paused)
        {
            return Paused();
        }

        var recall = await store.RecallAsync(new MemoryScope(user.Name, null, false, "api"), null, 50, cancellationToken).ConfigureAwait(false);
        return recall.Succeeded ? Entries(recall) : Failure(recall.Code, recall.Sentence);
    }

    private static async Task<IResult> RecallAsync(
        HttpContext context,
        IMemoryStore store,
        IOptions<MemoryOptions> options,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(user);
        var read = await CoworkBodies.ReadRequiredAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        MemoryRecallRequest? request;
        try
        {
            request = JsonSerializer.Deserialize(read.Bytes, MemoryJsonContext.Default.MemoryRecallRequest);
        }
        catch (JsonException)
        {
            return InvalidJson();
        }

        if (request is null)
        {
            return InvalidJson();
        }

        if (request.Query is not null && request.Query.Length > options.Value.MaxQuery)
        {
            return Failure(MemoryCodes.MemoryQueryTooLong, "The query is limited to 200 characters.");
        }

        if (options.Value.Paused)
        {
            return Paused();
        }

        var recall = await store.RecallAsync(
            new MemoryScope(user.Name, request.ProjectId, request.UseAccountMemory, "api"),
            request.Query,
            request.K,
            cancellationToken).ConfigureAwait(false);
        return recall.Succeeded ? Entries(recall) : Failure(recall.Code, recall.Sentence);
    }

    private static async Task<IResult> ResetAsync(HttpContext context, IMemoryStore store, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var read = await CoworkBodies.ReadOptionalAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        var mutation = await store.ResetAsync(user.Name, cancellationToken).ConfigureAwait(false);
        return mutation.Succeeded ? Results.NoContent() : Failure(mutation.Code, mutation.Sentence);
    }

    private static async Task<IResult> ExportAsync(IMemoryStore store, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var export = await store.ExportAsync(user.Name, cancellationToken).ConfigureAwait(false);
        if (!export.Succeeded || export.Json is null)
        {
            return Failure(export.Code, export.Sentence);
        }

        return Results.Text(export.Json, "application/json");
    }

    private static async Task<IResult> GetSettingsAsync(IMemoryStore store, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var settings = await store.GetSettingsAsync(user.Name, cancellationToken).ConfigureAwait(false);
        return Settings(settings);
    }

    private static async Task<IResult> PutSettingsAsync(HttpContext context, IMemoryStore store, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var read = await CoworkBodies.ReadRequiredAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null)
        {
            return read.Error;
        }

        MemorySettingsRecord? request;
        try
        {
            request = JsonSerializer.Deserialize(read.Bytes, MemoryJsonContext.Default.MemorySettingsRecord);
        }
        catch (JsonException)
        {
            return InvalidJson();
        }

        var settings = new MemorySettings(request?.Enabled ?? false, request?.Paused ?? false, request?.IncludeSensitive ?? false);
        await store.SetSettingsAsync(user.Name, settings, cancellationToken).ConfigureAwait(false);
        return Settings(settings);
    }

    private static async Task<IResult> GetAsync(string id, IMemoryStore store, IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var export = await store.ExportAsync(user.Name, cancellationToken).ConfigureAwait(false);
        if (!export.Succeeded || export.Json is null)
        {
            return Failure(export.Code, export.Sentence);
        }

        var document = JsonSerializer.Deserialize(export.Json, MemoryJsonContext.Default.MemoryExportDocument);
        var entry = document?.Entries.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        return entry is null
            ? WebProblems.Of(StatusCodes.Status404NotFound, "memory_not_found", "Memory not found.", "That memory entry was not found.")
            : Results.Text(JsonSerializer.Serialize(entry, MemoryJsonContext.Default.MemoryEntryRecord), "application/json");
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext context,
        string id,
        IMemoryStore store,
        IUserContext user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(user);
        var projectId = context.Request.Query["projectId"].ToString();
        var useAccount = string.Equals(context.Request.Query["useAccountMemory"], "true", StringComparison.OrdinalIgnoreCase);
        var scope = new MemoryScope(user.Name, string.IsNullOrEmpty(projectId) ? null : projectId, useAccount, "api");
        var mutation = await store.DeleteAsync(scope, id, cancellationToken).ConfigureAwait(false);
        if (mutation.Succeeded)
        {
            return Results.NoContent();
        }

        return string.Equals(mutation.Code, MemoryCodes.MemoryNotFound, StringComparison.Ordinal)
            ? WebProblems.Of(StatusCodes.Status404NotFound, "memory_not_found", "Memory not found.", mutation.Sentence ?? "That memory entry was not found.")
            : Failure(mutation.Code, mutation.Sentence);
    }

    private static IResult Entries(MemoryRecall recall)
    {
        var records = recall.Entries.Select(MemorySearch.ToRecord).ToList();
        var json = JsonSerializer.Serialize(new MemoryRecallResponse { Entries = records }, MemoryJsonContext.Default.MemoryRecallResponse);
        return Results.Text(json, "application/json");
    }

    private static IResult Settings(MemorySettings settings)
    {
        var json = JsonSerializer.Serialize(
            new MemorySettingsRecord { Enabled = settings.Enabled, Paused = settings.Paused, IncludeSensitive = settings.IncludeSensitive },
            MemoryJsonContext.Default.MemorySettingsRecord);
        return Results.Text(json, "application/json");
    }

    private static IResult SaveFailure(MemoryMutation mutation) =>
        string.Equals(mutation.Code, MemoryCodes.MemoryNotFound, StringComparison.Ordinal)
            ? Results.Text(
                "{\"type\":\"/problems/project_not_found\",\"title\":\"Project not found.\",\"status\":404,\"detail\":\"That project was not found.\"}",
                "application/problem+json", statusCode: StatusCodes.Status404NotFound)
            : Failure(mutation.Code, mutation.Sentence);

    private static IResult Failure(string code, string? sentence)
    {
        var status = string.Equals(code, MemoryCodes.MemoryQueryTooLong, StringComparison.Ordinal)
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status409Conflict;
        var record = new MemoryErrorRecord { Code = code, Sentence = sentence ?? "The memory request was refused." };
        var json = JsonSerializer.Serialize(record, MemoryJsonContext.Default.MemoryErrorRecord);
        return Results.Text(json, "application/json", statusCode: status);
    }

    private static IResult Paused() => Failure(MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written.");

    private static ProblemHttpResult Unavailable() =>
        WebProblems.Of(StatusCodes.Status503ServiceUnavailable, "memory_unavailable", "Memory is unavailable.", "Retry the request with the same idempotency key.");

    private static ProblemHttpResult InvalidJson() =>
        WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_json", "Invalid request body.", "The request body is not valid JSON.");

    private static ProblemHttpResult InvalidKey() =>
        WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_idempotency_key", "Invalid idempotency key.", "The Idempotency-Key header must appear at most once and hold 1 to 128 visible ASCII characters.");
}

/// <summary>Reads a Cowork JSON body and refuses an account or folder name.</summary>
internal static class CoworkBodies
{
    /// <summary>Reads a required JSON object.</summary>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bytes, or an error.</returns>
    public static async Task<CoworkBody> ReadRequiredAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var read = await ReadOptionalAsync(context, cancellationToken).ConfigureAwait(false);
        if (read.Error is not null || read.Bytes.Length > 0)
        {
            return read;
        }

        return new CoworkBody([], WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_json", "Invalid request body.", "The request body is required."));
    }

    /// <summary>Reads an optional JSON object.</summary>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bytes, or an error.</returns>
    public static async Task<CoworkBody> ReadOptionalAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.ContentLength is 0)
        {
            return new CoworkBody([], null);
        }

        if (context.Request.ContentType is not null && !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return new CoworkBody([], WebProblems.Of(StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type", "Unsupported media type.", "Content-Type must be application/json."));
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
                    return new CoworkBody([], WebProblems.Of(StatusCodes.Status413PayloadTooLarge, "payload_too_large", "Request body too large.", "The request body exceeds the configured limit."));
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
            return new CoworkBody(bytes, null);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (CoworkRequestBodies.NamesAccount(document.RootElement))
            {
                return new CoworkBody(bytes, WebProblems.Of(StatusCodes.Status400BadRequest, "owner_not_accepted", "Owner not accepted.", "The owner is the authenticated caller. The request body must not name an account."));
            }

            if (CoworkRequestBodies.NamesFolder(document.RootElement))
            {
                return new CoworkBody(bytes, WebProblems.Of(StatusCodes.Status400BadRequest, "folders_not_accepted", "Folders not accepted.", "Folders stay on the device. The request body must not name a folder."));
            }
        }
        catch (JsonException)
        {
            return new CoworkBody(bytes, WebProblems.Of(StatusCodes.Status400BadRequest, "invalid_json", "Invalid request body.", "The request body is not valid JSON."));
        }

        return new CoworkBody(bytes, null);
    }
}

/// <summary>A read body.</summary>
/// <param name="Bytes">The bytes.</param>
/// <param name="Error">The error, or null.</param>
internal readonly record struct CoworkBody(byte[] Bytes, IResult? Error);
