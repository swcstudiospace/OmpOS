using System.Collections.Frozen;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// Decides what <see cref="StageTimelineModel"/> announces: one polite sentence per stage transition, never for an unchanged status,
/// never for Pending, and never per token. Subagent (child) transitions are coalesced so at most one sentence per second
/// carries them, for example <c>3 subagents started.</c>
/// </summary>
internal sealed class StageAnnouncer
{
    private static readonly TimeSpan ChildInterval = TimeSpan.FromSeconds(1);
    private static readonly FrozenDictionary<(StageSource Source, StageStatus Status), string> Sentences = BuildSentences();

    private readonly TimeProvider _timeProvider;
    private int _started;
    private int _finished;
    private int _failed;
    private DateTimeOffset? _lastChildAnnouncement;

    /// <summary>Creates an announcer that reads the time from <paramref name="timeProvider"/>.</summary>
    /// <param name="timeProvider">The clock of the one-second child throttle.</param>
    internal StageAnnouncer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>Considers one event that changed the folded state.</summary>
    /// <param name="stageEvent">The new latest event for its key.</param>
    /// <param name="previous">The status the key had before, or null when the key is new.</param>
    /// <returns>The sentence to announce, or null when nothing is to be said now.</returns>
    internal string? Observe(StageEvent stageEvent, StageStatus? previous)
    {
        if (!Enum.IsDefined(stageEvent.Source) || !Enum.IsDefined(stageEvent.Status)
            || stageEvent.Status == StageStatus.Pending || previous == stageEvent.Status)
        {
            return null;
        }

        if (IsChild(stageEvent))
        {
            CountChild(stageEvent.Status);
            return ThrottledChildren();
        }

        var sentence = SentenceFor(stageEvent);
        var children = TakeChildren();
        return children is null ? sentence : $"{children} {sentence}";
    }

    /// <summary>Returns the pending subagent sentence at once.</summary>
    /// <returns>The sentence, or null when no subagent transition is pending.</returns>
    internal string? Flush()
    {
        var text = TakeChildren();
        if (text is not null)
        {
            _lastChildAnnouncement = _timeProvider.GetUtcNow();
        }

        return text;
    }

    private static bool IsChild(StageEvent stageEvent) =>
        !string.IsNullOrEmpty(stageEvent.ParentStageId)
        && !string.Equals(stageEvent.ParentStageId, stageEvent.StageId, StringComparison.Ordinal);

    private static string SentenceFor(StageEvent stageEvent)
    {
        if (!Sentences.TryGetValue((stageEvent.Source, stageEvent.Status), out var sentence))
        {
            return $"{PanelText.Plain(stageEvent.Title, StageTimelineModel.MaxTitleChars)}: {AgentPanelCopy.StageStatusText(stageEvent.Status)}.";
        }

        if (stageEvent.Source == StageSource.Ultrathink && stageEvent.Status == StageStatus.Succeeded)
        {
            var detail = PanelText.Plain(stageEvent.Detail, 120);
            if (detail.Length > 0)
            {
                return detail.EndsWith('.') ? $"{sentence} {detail}" : $"{sentence} {detail}.";
            }
        }

        return sentence;
    }

    private void CountChild(StageStatus status)
    {
        switch (status)
        {
            case StageStatus.Running:
                _started++;
                break;
            case StageStatus.Succeeded:
                _finished++;
                break;
            case StageStatus.Failed:
                _failed++;
                break;
            default:
                break;
        }
    }

    private string? ThrottledChildren()
    {
        var now = _timeProvider.GetUtcNow();
        if (_lastChildAnnouncement is { } last && now - last < ChildInterval)
        {
            return null;
        }

        var text = TakeChildren();
        if (text is not null)
        {
            _lastChildAnnouncement = now;
        }

        return text;
    }

    private string? TakeChildren()
    {
        var parts = new List<string>(3);
        if (_started > 0)
        {
            parts.Add($"{_started} {Subagents(_started)} started");
        }

        if (_finished > 0)
        {
            parts.Add($"{_finished} {Subagents(_finished)} finished");
        }

        if (_failed > 0)
        {
            parts.Add($"{_failed} {Subagents(_failed)} failed");
        }

        _started = 0;
        _finished = 0;
        _failed = 0;
        return parts.Count == 0 ? null : $"{string.Join(", ", parts)}.";
    }

    private static string Subagents(int count) => count == 1 ? "subagent" : "subagents";

    private static FrozenDictionary<(StageSource Source, StageStatus Status), string> BuildSentences()
    {
        var sentences = new Dictionary<(StageSource Source, StageStatus Status), string>();
        Add(sentences, StageSource.Omp, "The engine is running.", "The engine is ready.", "The engine is running with reduced function.", "The engine failed.", "The engine was skipped.", "The omp engine is not available.");
        Add(sentences, StageSource.Ultrathink, "Ultrathink is planning the prompt.", "Ultrathink planned.", "Ultrathink fell back to a plain spec.", "Ultrathink planning failed. The prompt ran as written.", "Ultrathink is off. Prompts run as written.", "Ultrathink is not available. Prompts run as written.");
        Add(sentences, StageSource.Swarm, "Swarm started.", "Swarm finished.", "Swarm is running with reduced function.", "Swarm stopped. Work continues as a single agent.", "Swarm is off. This agent runs alone.", "Swarm is not available. This agent runs alone.");
        Add(sentences, StageSource.Substrate, "Substrate is checking.", "Substrate is ok.", "Substrate answered with errors.", "Substrate failed. Steering is not recorded.", "Substrate is off. Steering is not recorded.", "Substrate is not reachable. Steering is not recorded.");
        Add(sentences, StageSource.Gsd, "GSD is reading the phase.", "GSD read the phase.", "GSD data is partial.", "GSD could not read the phase.", "GSD is off.", "GSD tools were not found.");
        return sentences.ToFrozenDictionary();
    }

    private static void Add(
        Dictionary<(StageSource Source, StageStatus Status), string> sentences,
        StageSource source,
        string running,
        string succeeded,
        string degraded,
        string failed,
        string skipped,
        string unavailable)
    {
        sentences[(source, StageStatus.Running)] = running;
        sentences[(source, StageStatus.Succeeded)] = succeeded;
        sentences[(source, StageStatus.Degraded)] = degraded;
        sentences[(source, StageStatus.Failed)] = failed;
        sentences[(source, StageStatus.Skipped)] = skipped;
        sentences[(source, StageStatus.Unavailable)] = unavailable;
    }
}
