using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// Reads shared memory, lessons and task facts for one session from the Substrate through <c>memory_search</c>,
/// <c>graph_get</c> and <c>issue_get</c>. The read is scope-bound: it queries only <c>graph:&lt;GraphId&gt;</c> of the session
/// and <c>repo:&lt;owner/name&gt;</c> of the workspace, always names the scope explicitly (an unscoped search would read every
/// project's memory), and discards any returned entry whose scope is not the requested one. Notes are untrusted plain text: they
/// are cleaned of control characters and capped at 500 characters. Every part that fails becomes a warning, never an exception.
/// </summary>
public sealed partial class SubstrateContextReader : ISubstrateContextReader
{
    /// <summary>The longest note text a snapshot carries, in UTF-16 units.</summary>
    public const int MaxNoteLength = 500;

    private const int MaxNotesPerScope = 20;
    private const int MaxLessons = 10;
    private const int MaxTitleLength = 200;

    private static readonly HashSet<string> MemoryKinds = new(["decision", "fact", "warning", "handoff", "todo"], StringComparer.Ordinal);

    private static readonly Action<ILogger, string, Exception?> PartFailed =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, "SubstrateContextPartFailed"),
            "A part of the Substrate context could not be read: {Part}.");

    private readonly SubstrateGateway _gateway;
    private readonly ILogger _logger;
    private readonly Func<string, string?> _repositoryResolver;

    /// <summary>Creates a reader over a gateway.</summary>
    /// <param name="gateway">The Substrate gateway.</param>
    /// <param name="logger">Receives the name of each part that failed, never a token or response text. Null discards it.</param>
    /// <param name="repositoryResolver">Names the repository of a workspace root as <c>owner/name</c>, or null. The git configuration of the workspace is read when null.</param>
    public SubstrateContextReader(SubstrateGateway gateway, ILogger? logger = null, Func<string, string?>? repositoryResolver = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        _gateway = gateway;
        _logger = logger ?? NullLogger.Instance;
        _repositoryResolver = repositoryResolver ?? WorkspaceRepositoryResolver.Resolve;
    }

    /// <inheritdoc />
    public async Task<SubstrateContextSnapshot> ReadAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var run = new ReadRun();
        var graphId = string.IsNullOrWhiteSpace(scope.GraphId) ? null : scope.GraphId;
        if (graphId is not null && !SafeIdentifier().IsMatch(graphId))
        {
            run.Warnings.Add("The Graph identifier is not valid, so no graph context was read.");
            graphId = null;
        }

        var repo = ResolveRepository(scope, run);

        if (graphId is not null)
        {
            var scopeName = "graph:" + graphId;
            await ReadMemoryAsync(run, scopeName, repo: null, subjectPrefix: null, MaxNotesPerScope, "graph memory", cancellationToken).ConfigureAwait(false);
        }

        if (repo is not null)
        {
            var scopeName = "repo:" + repo;
            await ReadMemoryAsync(run, scopeName, repo, subjectPrefix: null, MaxNotesPerScope, "repository memory", cancellationToken).ConfigureAwait(false);
            await ReadMemoryAsync(run, scopeName, repo, subjectPrefix: "lesson:", MaxLessons, "lessons", cancellationToken).ConfigureAwait(false);
        }

        if (graphId is not null)
        {
            await ReadTaskAsync(run, scope, graphId, cancellationToken).ConfigureAwait(false);
        }

        return new SubstrateContextSnapshot
        {
            SharedMemory = run.SharedMemory,
            Lessons = run.Lessons,
            TaskId = run.TaskId,
            Title = run.Title,
            KanbanStatus = run.KanbanStatus,
            Warnings = run.Warnings,
        };
    }

    private static bool IsLessonSubject(string? subject) =>
        subject is not null && subject.StartsWith("lesson", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ParseTime(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value) ? value : null;

    private static List<SubstrateWire.MemoryEntryDto>? ParseEntries(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return SubstrateWire.TryParse(trimmed, SubstrateJsonContext.Default.ListMemoryEntryDto);
        }

        return trimmed.StartsWith('{') ? SubstrateWire.TryParse(trimmed, SubstrateJsonContext.Default.MemoryPageDto)?.Entries : null;
    }

    private static SubstrateWire.GraphNodeDto? PickNode(SubstrateWire.GraphViewDto graph, StageObservationScope scope, ReadRun run)
    {
        var nodes = graph.Nodes?.Where(static node => node is { NodeId: not null }).Select(static node => node!).ToList() ?? [];
        if (nodes.Count == 0)
        {
            run.Warnings.Add("The graph has no task node.");
            return null;
        }

        var mine = nodes
            .Where(node => string.Equals(node.ClaimSession, scope.SessionId, StringComparison.Ordinal)
                || (scope.AcpSessionId is not null && string.Equals(node.ClaimSession, scope.AcpSessionId, StringComparison.Ordinal)))
            .ToList();
        if (mine.Count == 1)
        {
            return mine[0];
        }

        if (nodes.Count == 1)
        {
            return nodes[0];
        }

        run.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"The graph has {nodes.Count} task nodes and none is claimed by this session, so no task was chosen."));
        return null;
    }

    private string? ResolveRepository(StageObservationScope scope, ReadRun run)
    {
        string? repo;
        try
        {
            repo = _repositoryResolver(scope.WorkspaceRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            repo = null;
        }

        if (repo is null || !RepositoryName().IsMatch(repo))
        {
            run.Warnings.Add("The workspace repository could not be determined, so no repository memory or lessons were read.");
            return null;
        }

        return repo;
    }

    private async Task ReadMemoryAsync(ReadRun run, string scopeName, string? repo, string? subjectPrefix, int limit, string part, CancellationToken cancellationToken)
    {
        if (run.Unreachable)
        {
            return;
        }

        try
        {
            var arguments = SubstrateToolArguments.Create(
                ("query", subjectPrefix is null ? string.Empty : null),
                ("scope", scopeName),
                ("limit", limit),
                ("repo", repo),
                ("subject_prefix", subjectPrefix));
            var result = await _gateway.CallToolAsync("memory_search", arguments, cancellationToken).ConfigureAwait(false);
            if (result.IsError)
            {
                Warn(run, part, "was refused by the server");
                return;
            }

            var entries = ParseEntries(result.Text);
            if (entries is null)
            {
                Warn(run, part, "returned an unreadable answer");
                return;
            }

            CollectNotes(run, entries, scopeName, subjectPrefix is not null, limit, part);
        }
        catch (SubstrateUnavailableException ex)
        {
            Warn(run, part, string.Create(CultureInfo.InvariantCulture, $"could not be read ({ex.Reason})"));
            run.Unreachable = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warn(run, part, "could not be read");
        }
    }

    private static void CollectNotes(ReadRun run, List<SubstrateWire.MemoryEntryDto> entries, string scopeName, bool lessons, int limit, string part)
    {
        var foreign = 0;
        var added = 0;
        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Text))
            {
                continue;
            }

            if (!string.Equals(entry.Scope, scopeName, StringComparison.Ordinal))
            {
                foreign++;
                continue;
            }

            if (entry.Expired == true || !string.IsNullOrEmpty(entry.SupersededBy))
            {
                continue;
            }

            if (AddNote(run, entry, scopeName, lessons) && ++added >= limit)
            {
                break;
            }
        }

        if (foreign > 0)
        {
            run.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"Ignored {foreign} {part} entries that belong to another scope."));
        }
    }

    private static bool AddNote(ReadRun run, SubstrateWire.MemoryEntryDto entry, string scopeName, bool lesson)
    {
        var kind = entry.Kind ?? string.Empty;
        if (lesson)
        {
            if ((entry.Kind is not null && !string.Equals(kind, "fact", StringComparison.Ordinal))
                || entry.Subject is null
                || !entry.Subject.StartsWith("lesson:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var text = SubstrateText.Sanitize(SubstrateText.StripLessonHeader(entry.Text!), MaxNoteLength);
            if (text.Length == 0)
            {
                return false;
            }

            run.Lessons.Add(Note(entry, text, scopeName + " (lesson)"));
            return true;
        }

        if (!MemoryKinds.Contains(kind) || IsLessonSubject(entry.Subject))
        {
            return false;
        }

        var body = SubstrateText.Sanitize(entry.Text, MaxNoteLength);
        if (body.Length == 0)
        {
            return false;
        }

        run.SharedMemory.Add(Note(entry, body, string.Create(CultureInfo.InvariantCulture, $"{scopeName} ({kind})")));
        return true;
    }

    private static ContextNote Note(SubstrateWire.MemoryEntryDto entry, string text, string origin) =>
        new()
        {
            Id = SubstrateText.Sanitize(entry.Id, 80),
            Text = text,
            Origin = origin,
            RecordedAt = ParseTime(entry.Created),
        };

    private void Warn(ReadRun run, string part, string what)
    {
        PartFailed(_logger, part, null);
        run.Warnings.Add(string.Create(CultureInfo.InvariantCulture, $"The Substrate {part} {what}."));
    }

    private async Task ReadTaskAsync(ReadRun run, StageObservationScope scope, string graphId, CancellationToken cancellationToken)
    {
        if (run.Unreachable)
        {
            return;
        }

        try
        {
            var graphResult = await _gateway.CallToolAsync("graph_get", SubstrateToolArguments.Create(("graph_id", graphId)), cancellationToken).ConfigureAwait(false);
            if (graphResult.IsError || !graphResult.Text.TrimStart().StartsWith('{'))
            {
                Warn(run, "task", "has no graph record in the index");
                return;
            }

            var graph = SubstrateWire.TryParse(graphResult.Text, SubstrateJsonContext.Default.GraphViewDto);
            if (graph is null)
            {
                Warn(run, "task", "returned an unreadable graph");
                return;
            }

            var node = PickNode(graph, scope, run);
            if (node is null)
            {
                return;
            }

            run.TaskId = SubstrateText.Sanitize(node.NodeId, 80);
            var status = SubstrateText.Sanitize(node.State, 40);
            run.KanbanStatus = status.Length == 0 ? null : status;

            var issueResult = await _gateway.CallToolAsync("issue_get", SubstrateToolArguments.Create(("graph_id", graphId), ("node_id", node.NodeId)), cancellationToken).ConfigureAwait(false);
            var issue = issueResult.IsError ? null : SubstrateWire.TryParse(issueResult.Text, SubstrateJsonContext.Default.IssueDto);
            var title = issue is { Found: true } ? SubstrateText.Sanitize(issue.Title, MaxTitleLength) : string.Empty;
            if (title.Length > 0)
            {
                run.Title = title;
            }
            else
            {
                Warn(run, "task title", "is not available");
            }
        }
        catch (SubstrateUnavailableException ex)
        {
            Warn(run, "task", string.Create(CultureInfo.InvariantCulture, $"could not be read ({ex.Reason})"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Warn(run, "task", "could not be read");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,200}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeIdentifier();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}/[A-Za-z0-9._-]{1,100}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepositoryName();

    private sealed class ReadRun
    {
        internal List<ContextNote> SharedMemory { get; } = [];

        internal List<ContextNote> Lessons { get; } = [];

        internal List<string> Warnings { get; } = [];

        internal string? TaskId { get; set; }

        internal string? Title { get; set; }

        internal string? KanbanStatus { get; set; }

        internal bool Unreachable { get; set; }
    }
}
