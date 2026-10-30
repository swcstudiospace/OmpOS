using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Artifacts.Model;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Cowork.Artifacts.Tools;

/// <summary>
/// Host-owned artifact tool family. omp sees <c>mcp__cowork_artifact_*</c> only after the gateway is configured; this type does not talk to omp.
/// </summary>
public sealed class ArtifactToolProvider : ICoworkToolProvider
{
    /// <summary>The provider id.</summary>
    public const string Id = "artifact";

    /// <summary>The grant scope returned on create and update previews.</summary>
    public const string GrantScope = "artifact.write";

    /// <summary>The grant value returned on create and update previews.</summary>
    public const string GrantValue = "cowork";

    private const int MaxList = 50;
    private static readonly JsonDocument CreateSchema = JsonDocument.Parse("""{"type":"object","additionalProperties":false,"properties":{"title":{"type":"string"},"kind":{"type":"string"},"language":{"type":"string"},"content":{"type":"string"},"from_file":{"type":"string"},"media_type":{"type":"string"},"change_note":{"type":"string"},"permissions":{"type":"object"}}}""");
    private static readonly JsonDocument UpdateSchema = JsonDocument.Parse("""{"type":"object","additionalProperties":false,"properties":{"artifact_id":{"type":"string"},"base_version":{"type":"integer"},"content":{"type":"string"},"from_file":{"type":"string"},"edits":{"type":"array"},"title":{"type":"string"},"change_note":{"type":"string"},"permissions":{"type":"object"}}}""");
    private static readonly JsonDocument ReadSchema = JsonDocument.Parse("""{"type":"object","additionalProperties":false,"properties":{"artifact_id":{"type":"string"},"version":{"type":"integer"},"max_bytes":{"type":"integer"}}}""");
    private static readonly JsonDocument ListSchema = JsonDocument.Parse("""{"type":"object","additionalProperties":false,"properties":{"session_id":{"type":"string"},"kind":{"type":"string"},"limit":{"type":"integer"},"cursor":{"type":"string"}}}""");

    private readonly ArtifactService _service;
    private readonly ArtifactWriteLimiter _limiter;
    private readonly IOptions<ArtifactOptions> _options;
    private readonly string? _storageFolder;
    private readonly ConcurrentDictionary<string, IndexedArtifact> _index = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ArtifactToolProvider"/> class.</summary>
    /// <param name="service">The artifact core.</param>
    /// <param name="limiter">The per-session write limiter.</param>
    /// <param name="options">The bound limits.</param>
    /// <param name="storageFolder">The Storage folder, in addition to trusted folders, or null.</param>
    public ArtifactToolProvider(ArtifactService service, ArtifactWriteLimiter limiter, IOptions<ArtifactOptions> options, string? storageFolder = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(options);
        _service = service;
        _limiter = limiter;
        _options = options;
        _storageFolder = storageFolder;
    }

    /// <inheritdoc />
    public string ProviderId => Id;

    /// <inheritdoc />
    public IReadOnlyList<CoworkToolDescriptor> Describe() =>
    [
        Descriptor("artifact_create", "Create an artifact. Nothing in this family deletes, shares or publishes.", CoworkToolEffect.Actuating, CreateSchema),
        Descriptor("artifact_update", "Update an artifact by content, file or exact-once edits.", CoworkToolEffect.Actuating, UpdateSchema),
        Descriptor("artifact_read", "Read one artifact version. Image reads return metadata only.", CoworkToolEffect.ReadOnly, ReadSchema),
        Descriptor("artifact_list", "List artifacts for this host. Limit is at most 50.", CoworkToolEffect.ReadOnly, ListSchema),
    ];

    /// <inheritdoc />
    public ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new CoworkProviderState(CoworkProviderAvailability.Available, null));
    }

    /// <inheritdoc />
    public ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();
        var writing = invocation.ToolName is "artifact_create" or "artifact_update";
        var title = ReadString(invocation.Arguments, "title");
        var kind = ReadString(invocation.Arguments, "kind");
        var details = new List<string>();
        if (title is not null)
        {
            details.Add("title=" + title);
        }

        if (kind is not null)
        {
            details.Add("kind=" + kind);
        }

        details.Add("size=" + ObservedSize(invocation).ToString(CultureInfo.InvariantCulture));
        CoworkGrantKey? grant = writing ? new CoworkGrantKey(GrantScope, GrantValue) : null;
        return ValueTask.FromResult(new CoworkCallPreview(invocation.ToolName, invocation.SessionId, details, grant));
    }

    /// <inheritdoc />
    public async ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return invocation.ToolName switch
            {
                "artifact_create" => await CreateAsync(session, invocation, cancellationToken).ConfigureAwait(false),
                "artifact_update" => await UpdateAsync(session, invocation, cancellationToken).ConfigureAwait(false),
                "artifact_read" => await ReadAsync(invocation, cancellationToken).ConfigureAwait(false),
                "artifact_list" => List(session, invocation),
                _ => Error("unknown_tool"),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return Error("file_not_allowed");
        }
        catch (UnauthorizedAccessException)
        {
            return Error("file_not_allowed");
        }
    }

    /// <inheritdoc />
    public ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        _limiter.Forget(sessionId);
        return ValueTask.CompletedTask;
    }

    private async Task<CoworkToolResult> CreateAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (!TryKind(ReadString(invocation.Arguments, "kind"), out var kind))
        {
            return Error("kind_invalid");
        }

        if (!ArtifactOriginRules.TryRead(invocation.Arguments, out var origins, out var warnings, out var originError))
        {
            return Error(originError!);
        }

        if (!TryBytes(session, invocation.Arguments, kind, out var bytes, out var fileError))
        {
            return Error(fileError!);
        }

        if (!TryAcquire(session.SessionId, out var limited))
        {
            return limited!;
        }

        var created = await _service.CreateAsync(
            kind,
            ReadString(invocation.Arguments, "title") ?? string.Empty,
            bytes,
            session.SessionId,
            ReadString(invocation.Arguments, "language"),
            ReadString(invocation.Arguments, "media_type"),
            ReadString(invocation.Arguments, "change_note"),
            ArtifactAuthor.Agent,
            cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess || created.Value is null)
        {
            return Error(created.Failure?.Code ?? "rejected");
        }

        var allWarnings = warnings.Concat(RendererWarnings(kind, bytes)).ToArray();
        _index[created.Value.ArtifactId] = new IndexedArtifact(created.Value.ArtifactId, session.SessionId, kind);
        return CoworkToolResult.Text(Success(created.Value, allWarnings), isUntrusted: false);
    }

    private async Task<CoworkToolResult> UpdateAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        var id = ReadString(invocation.Arguments, "artifact_id");
        if (string.IsNullOrEmpty(id) || !invocation.Arguments.TryGetProperty("base_version", out var baseVersion) || baseVersion.ValueKind != JsonValueKind.Number)
        {
            return Error("arguments_invalid");
        }

        if (!ArtifactOriginRules.TryRead(invocation.Arguments, out _, out var warnings, out var originError))
        {
            return Error(originError!);
        }

        var current = await _service.ReadAsync(id, baseVersion.GetInt32(), cancellationToken).ConfigureAwait(false);
        if (!current.IsSuccess || current.Value is null)
        {
            return Error(current.Failure?.Code ?? "not_found");
        }

        if (!TryUpdateBytes(session, invocation.Arguments, current.Value, out var bytes, out var fileError))
        {
            return Error(fileError!);
        }

        if (!TryAcquire(session.SessionId, out var limited))
        {
            return limited!;
        }

        var updated = await _service.UpdateAsync(id, baseVersion.GetInt32(), bytes, ReadString(invocation.Arguments, "change_note"), ArtifactAuthor.Agent, cancellationToken).ConfigureAwait(false);
        if (!updated.IsSuccess || updated.Value is null)
        {
            var code = updated.Failure?.Code ?? "rejected";
            if (updated.Failure?.Latest is int latest)
            {
                return Error(code, "\"latest_version\":" + latest.ToString(CultureInfo.InvariantCulture));
            }

            return Error(code);
        }

        var allWarnings = warnings.Concat(RendererWarnings(updated.Value.Kind, bytes)).ToArray();
        _index[updated.Value.ArtifactId] = new IndexedArtifact(updated.Value.ArtifactId, session.SessionId, updated.Value.Kind);
        return CoworkToolResult.Text(Success(updated.Value, allWarnings), isUntrusted: false);
    }

    private async Task<CoworkToolResult> ReadAsync(CoworkToolInvocation invocation, CancellationToken cancellationToken)
    {
        var id = ReadString(invocation.Arguments, "artifact_id");
        if (string.IsNullOrEmpty(id))
        {
            return Error("arguments_invalid");
        }

        var number = invocation.Arguments.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
            ? version.GetInt32()
            : await LatestAsync(id, cancellationToken).ConfigureAwait(false);
        var read = await _service.ReadAsync(id, number, cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess || read.Value is null)
        {
            return Error(read.Failure?.Code ?? "not_found");
        }

        if (read.Value.Kind == ArtifactKind.Image)
        {
            return CoworkToolResult.Text(
                "{\"artifact_id\":\"" + read.Value.ArtifactId + "\",\"version\":" + read.Value.Number.ToString(CultureInfo.InvariantCulture) + ",\"bytes\":" + read.Value.Content.Length.ToString(CultureInfo.InvariantCulture) + ",\"media_type\":\"" + read.Value.MediaType + "\"}",
                isUntrusted: false);
        }

        var text = Encoding.UTF8.GetString(read.Value.Content.Span);
        if (invocation.Arguments.TryGetProperty("max_bytes", out var max) && max.TryGetInt32(out var cap) && cap >= 0 && text.Length > cap)
        {
            text = text[..cap];
        }

        return new CoworkToolResult([new CoworkTextBlock("<untrusted>\n" + text + "\n</untrusted>", true)], false);
    }

    private CoworkToolResult List(CoworkSessionContext session, CoworkToolInvocation invocation)
    {
        var limit = MaxList;
        if (invocation.Arguments.TryGetProperty("limit", out var rawLimit) && rawLimit.TryGetInt32(out var requested))
        {
            if (requested is < 1 or > MaxList)
            {
                return Error("arguments_invalid");
            }

            limit = requested;
        }

        var offset = 0;
        var cursor = ReadString(invocation.Arguments, "cursor");
        if (cursor is not null && !int.TryParse(cursor, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset))
        {
            return Error("arguments_invalid");
        }

        var sessionFilter = ReadString(invocation.Arguments, "session_id") ?? session.SessionId;
        ArtifactKind? kindFilter = null;
        if (ReadString(invocation.Arguments, "kind") is string kindText)
        {
            if (!TryKind(kindText, out var parsed))
            {
                return Error("kind_invalid");
            }

            kindFilter = parsed;
        }

        var rows = _index.Values
            .Where(row => string.Equals(row.SessionId, sessionFilter, StringComparison.Ordinal))
            .Where(row => kindFilter is null || row.Kind == kindFilter)
            .OrderBy(row => row.Id, StringComparer.Ordinal)
            .Skip(offset)
            .Take(limit)
            .Select(row => row.Id)
            .ToArray();
        var next = offset + rows.Length;
        var listed = string.Join(',', rows.Select(static id => "\"" + id + "\""));
        return CoworkToolResult.Text("{\"artifacts\":[" + listed + "],\"next_cursor\":\"" + next.ToString(CultureInfo.InvariantCulture) + "\"}", isUntrusted: false);
    }

    private bool TryAcquire(string sessionId, out CoworkToolResult? limited)
    {
        if (_limiter.TryAcquire(sessionId, _options.Value.MaxWritesPerMinutePerSession, out var retry))
        {
            limited = null;
            return true;
        }

        limited = Error("rate_limited", "\"retry-after\":" + Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        return false;
    }

    private bool TryUpdateBytes(CoworkSessionContext session, JsonElement arguments, ArtifactSnapshot current, out byte[] bytes, out string? error)
    {
        var modes = 0;
        if (arguments.TryGetProperty("content", out _))
        {
            modes++;
        }

        if (arguments.TryGetProperty("from_file", out _))
        {
            modes++;
        }

        if (arguments.TryGetProperty("edits", out _))
        {
            modes++;
        }

        if (modes != 1)
        {
            bytes = [];
            error = "arguments_invalid";
            return false;
        }

        if (arguments.TryGetProperty("edits", out var edits))
        {
            if (current.Kind == ArtifactKind.Image)
            {
                bytes = [];
                error = "edits_text_only";
                return false;
            }

            if (!TryEdits(edits, out var parsed, out error))
            {
                bytes = [];
                return false;
            }

            if (!EditApplier.TryApply(Encoding.UTF8.GetString(current.Content.Span), parsed, out var updated, out error))
            {
                bytes = [];
                return false;
            }

            bytes = Encoding.UTF8.GetBytes(updated);
            return true;
        }

        return TryBytes(session, arguments, current.Kind, out bytes, out error);
    }

    private bool TryBytes(CoworkSessionContext session, JsonElement arguments, ArtifactKind kind, out byte[] bytes, out string? error)
    {
        var hasContent = arguments.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String;
        var hasFile = arguments.TryGetProperty("from_file", out var file) && file.ValueKind == JsonValueKind.String;
        if (hasContent == hasFile)
        {
            bytes = [];
            error = "arguments_invalid";
            return false;
        }

        if (hasFile)
        {
            if (!TryReadFile(session, file.GetString() ?? string.Empty, out bytes))
            {
                error = "file_not_allowed";
                return false;
            }

            error = null;
            return true;
        }

        var text = content.GetString() ?? string.Empty;
        if (kind == ArtifactKind.Image)
        {
            try
            {
                bytes = Convert.FromBase64String(text);
            }
            catch (FormatException)
            {
                bytes = [];
                error = "content_invalid";
                return false;
            }
        }
        else
        {
            bytes = Encoding.UTF8.GetBytes(text);
        }

        error = null;
        return true;
    }

    private bool TryReadFile(CoworkSessionContext session, string path, out byte[] bytes)
    {
        bytes = [];
        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
            if (File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            {
                var target = File.ResolveLinkTarget(full, returnFinalTarget: true);
                if (target is null)
                {
                    return false;
                }

                full = target.FullName;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (!File.Exists(full) || Directory.Exists(full))
        {
            return false;
        }

        if (!IsGranted(session, full))
        {
            return false;
        }

        bytes = File.ReadAllBytes(full);
        return true;
    }

    private bool IsGranted(CoworkSessionContext session, string full)
    {
        foreach (var folder in session.TrustedFolders)
        {
            if (IsUnder(full, folder))
            {
                return true;
            }
        }

        return _storageFolder is not null && IsUnder(full, _storageFolder);
    }

    private static bool IsUnder(string full, string folder)
    {
        var root = Path.GetFullPath(folder);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        return full.StartsWith(root, StringComparison.Ordinal) || string.Equals(full, Path.TrimEndingDirectorySeparator(root), StringComparison.Ordinal);
    }

    private async Task<int> LatestAsync(string id, CancellationToken cancellationToken)
    {
        var all = await _service.ReadAllAsync(id, cancellationToken).ConfigureAwait(false);
        return all.IsSuccess && all.Value is { Count: > 0 } versions ? versions[^1].Number : 1;
    }

    private static bool TryEdits(JsonElement edits, out List<EditApplier.Edit> parsed, out string? error)
    {
        parsed = [];
        error = null;
        if (edits.ValueKind != JsonValueKind.Array)
        {
            error = "arguments_invalid";
            return false;
        }

        foreach (var edit in edits.EnumerateArray())
        {
            if (!edit.TryGetProperty("old", out var old) || !edit.TryGetProperty("new", out var replacement) || old.ValueKind != JsonValueKind.String || replacement.ValueKind != JsonValueKind.String)
            {
                error = "arguments_invalid";
                return false;
            }

            parsed.Add(new EditApplier.Edit(old.GetString() ?? string.Empty, replacement.GetString() ?? string.Empty));
        }

        return true;
    }

    private static List<string> RendererWarnings(ArtifactKind kind, byte[] bytes)
    {
        var warnings = new List<string>();
        if (kind == ArtifactKind.Svg)
        {
            var sanitized = SvgSanitizer.Sanitize(Encoding.UTF8.GetString(bytes));
            if (!sanitized.Accepted || sanitized.Findings.Count > 0)
            {
                warnings.Add("svg_elements_removed");
            }
        }

        return warnings;
    }

    private static string Success(ArtifactSnapshot snapshot, IReadOnlyList<string> warnings)
    {
        var listed = string.Join(',', warnings.Select(static warning => "\"" + warning + "\""));
        return "{\"artifact_id\":\"" + snapshot.ArtifactId + "\",\"version\":" + snapshot.Number.ToString(CultureInfo.InvariantCulture) + ",\"content_sha256\":\"" + snapshot.ContentSha256 + "\",\"bytes\":" + snapshot.Content.Length.ToString(CultureInfo.InvariantCulture) + ",\"warnings\":[" + listed + "]}";
    }

    private static CoworkToolResult Error(string code, string? extra = null)
    {
        var body = extra is null ? "{\"error\":\"" + code + "\"}" : "{\"error\":\"" + code + "\"," + extra + "}";
        return CoworkToolResult.Failure(body);
    }

    private static int ObservedSize(CoworkToolInvocation invocation)
    {
        if (invocation.Arguments.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString()?.Length ?? 0;
        }

        return 0;
    }

    private static string? ReadString(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryKind(string? text, out ArtifactKind kind) => Enum.TryParse(text, ignoreCase: false, out kind);

    private static CoworkToolDescriptor Descriptor(string name, string description, CoworkToolEffect effect, JsonDocument schema) =>
        new()
        {
            Name = name,
            ProviderId = Id,
            Description = description,
            InputSchema = schema.RootElement,
            Effect = effect,
            Approval = CoworkApprovalRequirement.ByPolicy,
            Results = CoworkResultKinds.Text,
        };

    private sealed record IndexedArtifact(string Id, string SessionId, ArtifactKind Kind);
}

