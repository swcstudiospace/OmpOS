using System.Collections.ObjectModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Agents;

/// <summary>
/// One agent run in the Agents window (ADE-05). It owns its transcript, so two runs never share transcript state. Only
/// <see cref="AgentRunRegistry"/> changes its status, through the transition table. Opening or closing its view does not touch it.
/// </summary>
public sealed class AgentRun : ObservableModel
{
    private readonly ObservableCollection<string> _transcript = [];
    private AgentRunStatus _status = AgentRunStatus.Running;

    internal AgentRun(string id, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = id;
        Title = title;
        Transcript = new ReadOnlyObservableCollection<string>(_transcript);
    }

    /// <summary>Gets the run identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the status of the run.</summary>
    public AgentRunStatus Status => _status;

    /// <summary>Gets the status as display text.</summary>
    public string StatusText => _status.ToString();

    /// <summary>Gets the row name the Agents window exposes to assistive technology: the title and the status text.</summary>
    public string AccessibleLabel => $"{Title}, {StatusText}";

    /// <summary>Gets this run's transcript, in the order its entries were appended.</summary>
    public ReadOnlyObservableCollection<string> Transcript { get; }

    /// <summary>Appends one entry to this run's transcript. It does not change the status.</summary>
    /// <param name="entry">The transcript entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public void AppendTranscript(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _transcript.Add(entry);
    }

    /// <summary>Records a status that the registry's transition table has allowed.</summary>
    /// <param name="status">The new status.</param>
    internal void SetStatus(AgentRunStatus status)
    {
        if (!SetProperty(ref _status, status, nameof(Status)))
        {
            return;
        }

        NotifyPropertyChanged(nameof(StatusText));
        NotifyPropertyChanged(nameof(AccessibleLabel));
    }
}
