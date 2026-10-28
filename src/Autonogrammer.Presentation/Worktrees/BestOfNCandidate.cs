using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Worktrees;

/// <summary>One candidate of a best-of-n run, for the comparison list.</summary>
public sealed class BestOfNCandidate : ObservableModel
{
    private BestOfNState _state = BestOfNState.Waiting;
    private string _summary = string.Empty;
    private int _changedFiles;
    private WorktreeRunInfo? _run;

    /// <summary>Creates a waiting candidate.</summary>
    /// <param name="index">The candidate's number, from 1.</param>
    /// <param name="runId">The run id its worktree has or would have.</param>
    public BestOfNCandidate(int index, string runId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        Index = index;
        RunId = runId;
    }

    /// <summary>Gets the candidate's number, from 1.</summary>
    public int Index { get; }

    /// <summary>Gets the run id of the candidate's worktree.</summary>
    public string RunId { get; }

    /// <summary>Gets the worktree, or <see langword="null"/> when it could not be made.</summary>
    public WorktreeRunInfo? Run => _run;

    /// <summary>Gets where the candidate stands.</summary>
    public BestOfNState State => _state;

    /// <summary>Gets what the agent said it did, or why it did not finish.</summary>
    public string Summary => _summary;

    /// <summary>Gets the number of files the candidate changed.</summary>
    public int ChangedFiles => _changedFiles;

    /// <summary>Gets the row text: the state, the files changed and the summary.</summary>
    public string RowText => WorktreeCommandsCopy.CandidateRow(Index, _state, _changedFiles, _summary);

    internal void SetRun(WorktreeRunInfo run)
    {
        _run = run;
        NotifyPropertyChanged(nameof(Run));
    }

    internal void Update(BestOfNState state, string summary, int changedFiles)
    {
        _state = state;
        _summary = summary;
        _changedFiles = changedFiles;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Summary));
        NotifyPropertyChanged(nameof(ChangedFiles));
        NotifyPropertyChanged(nameof(RowText));
    }
}
