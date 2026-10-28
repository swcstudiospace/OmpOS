using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Worktrees;

/// <summary>One run worktree in the list.</summary>
public sealed class WorktreeRunRow : ObservableModel
{
    /// <summary>Creates a row.</summary>
    /// <param name="run">The worktree.</param>
    /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
    public WorktreeRunRow(WorktreeRunInfo run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Run = run;
    }

    /// <summary>Gets the worktree.</summary>
    public WorktreeRunInfo Run { get; }

    /// <summary>Gets the run id. It is the parameter of the row's commands.</summary>
    public string RunId => Run.RunId;

    /// <summary>Gets the row text: the run, its branch and how many files it changed.</summary>
    public string RowText => WorktreeCommandsCopy.RunRow(Run);

    /// <summary>Gets the accessible name of the row.</summary>
    public string RowName => RowText;

    /// <summary>Gets the accessible name of the row's Preview control.</summary>
    public string PreviewName => WorktreeCommandsCopy.PreviewName(RunId);

    /// <summary>Gets the accessible name of the row's Apply control.</summary>
    public string ApplyName => WorktreeCommandsCopy.ApplyName(RunId);

    /// <summary>Gets the accessible name of the row's Delete control.</summary>
    public string DeleteName => WorktreeCommandsCopy.DeleteName(RunId);
}
