using System.Collections.Concurrent;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// The identity of one conversation as hooks see it: the common input fields, the environment <c>sessionStart</c> hooks
/// returned, the current generation and the follow-up counters. One instance lives as long as the conversation.
/// </summary>
public sealed class HookSession
{
    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<string, int> _loopCounts = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, string> _environment = new Dictionary<string, string>(StringComparer.Ordinal);
    private string _generationId;

    /// <summary>
    /// Starts a session.
    /// </summary>
    /// <param name="conversationId">The stable conversation id (the ACP session id); becomes <c>conversation_id</c>.</param>
    /// <param name="workspaceRoots">The absolute workspace roots, primary first.</param>
    public HookSession(string conversationId, IReadOnlyList<string> workspaceRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(workspaceRoots);
        ConversationId = conversationId;
        WorkspaceRoots = workspaceRoots;
        _generationId = NewId();
    }

    /// <summary>The stable conversation id.</summary>
    public string ConversationId { get; }

    /// <summary>The absolute workspace roots, primary first. Empty for sessions without a workspace.</summary>
    public IReadOnlyList<string> WorkspaceRoots { get; }

    /// <summary>The primary workspace root, or the empty string when there is none.</summary>
    public string ProjectDirectory => WorkspaceRoots.Count > 0 ? WorkspaceRoots[0] : string.Empty;

    /// <summary>The legacy model slug, sent as <c>model</c>.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>The structured model id, sent as <c>model_id</c> when set.</summary>
    public string? ModelId { get; init; }

    /// <summary>The selected model parameters, sent as <c>model_params</c> when any.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ModelParameters { get; init; } = [];

    /// <summary>The signed-in user's email, or null.</summary>
    public string? UserEmail { get; init; }

    /// <summary>The path of the conversation transcript, or null when the host does not know it.</summary>
    public string? TranscriptPath { get; init; }

    /// <summary>The compatibility version string sent as <c>cursor_version</c> and <c>CURSOR_VERSION</c>, for example <c>ompos-0.15.0</c>.</summary>
    public string CursorVersion { get; init; } = "ompos";

    /// <summary>True for remote and hosted sessions: user hooks do not apply and <c>CURSOR_CODE_REMOTE</c> is set.</summary>
    public bool IsRemote { get; init; }

    /// <summary>The generation id: it changes with every user message and is shared by every hook of that prompt.</summary>
    public string GenerationId
    {
        get
        {
            lock (_gate)
            {
                return _generationId;
            }
        }
    }

    /// <summary>The environment variables <c>sessionStart</c> hooks returned; passed to every later hook of the session.</summary>
    public IReadOnlyDictionary<string, string> SessionEnvironment
    {
        get
        {
            lock (_gate)
            {
                return _environment;
            }
        }
    }

    /// <summary>
    /// Starts a new generation for a new user message and resets the follow-up counters, so each user prompt gets a fresh
    /// <c>loop_limit</c> budget.
    /// </summary>
    /// <returns>The new generation id.</returns>
    public string NewGeneration()
    {
        var id = NewId();
        lock (_gate)
        {
            _generationId = id;
        }

        _loopCounts.Clear();
        return id;
    }

    /// <summary>
    /// Merges environment variables returned by a <c>sessionStart</c> hook into the session environment.
    /// </summary>
    /// <param name="values">The variables to add or replace.</param>
    public void MergeEnvironment(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            var merged = new Dictionary<string, string>(_environment, StringComparer.Ordinal);
            foreach (var pair in values)
            {
                merged[pair.Key] = pair.Value;
            }

            _environment = merged;
        }
    }

    /// <summary>
    /// Returns how many follow-ups a hook has already triggered since the last user message.
    /// </summary>
    /// <param name="hookId">The hook's identifier.</param>
    /// <returns>The count; zero when none.</returns>
    internal int LoopCount(string hookId) => _loopCounts.TryGetValue(hookId, out var count) ? count : 0;

    /// <summary>
    /// Records the follow-up counter of a hook.
    /// </summary>
    /// <param name="hookId">The hook's identifier.</param>
    /// <param name="count">The new count.</param>
    internal void SetLoopCount(string hookId, int count) => _loopCounts[hookId] = count;

    private static string NewId() => Guid.NewGuid().ToString("D");
}
