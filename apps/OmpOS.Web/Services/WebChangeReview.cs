using System.Collections.Specialized;
using System.ComponentModel;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Changes;
using ChangeKind = OmpOS.Contracts.Controls.ChangeKind;

namespace OmpOS.Web.Services;

/// <summary>
/// The Changes bar of one web session. The API owns the truth (<c>GET /sessions/{id}/changes</c>): <see cref="RefreshAsync"/> shows the pending
/// files in a <see cref="ChangeSetModel"/> over a <see cref="WebWorkspaceFileSystem"/>, and a Keep or Undo the user chooses in the model is sent to
/// <c>POST /sessions/{id}/changes/{path}/accept|reject</c>. A conflict or refusal from the API becomes <see cref="Notice"/> and a fresh list.
/// </summary>
internal sealed class WebChangeReview : IDisposable
{
    private readonly IOmpApiClient _api;
    private readonly string _sessionId;
    private readonly WebWorkspaceFileSystem _files = new();
    private readonly Dictionary<string, ChangeStatus> _sent = new(StringComparer.Ordinal);
    private readonly HashSet<ChangeEntry> _watched = [];
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="WebChangeReview"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="sessionId">The session.</param>
    internal WebChangeReview(IOmpApiClient api, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _api = api;
        _sessionId = sessionId;
        Model = new ChangeSetModel(_files);
        ((INotifyCollectionChanged)Model.Entries).CollectionChanged += OnEntriesChanged;
    }

    /// <summary>Occurs after <see cref="Notice"/> changed.</summary>
    internal event EventHandler? NoticeChanged;

    /// <summary>Gets the review model the Changes bar binds to.</summary>
    internal ChangeSetModel Model { get; }

    /// <summary>Gets the sentence that explains the last refused or unapplied decision, or null.</summary>
    internal string? Notice { get; private set; }

    /// <summary>Reads the changes again and shows the pending ones.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the model was updated. An unreachable API leaves the model as it was.</returns>
    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        SessionChangesResponse response;
        try
        {
            response = await _api.GetChangesAsync(_sessionId, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            SetNotice(WebFailureText.Describe(fault));
            return;
        }

        var pending = response.Changes.Where(static change => change.Status == ChangeReviewStatus.Pending).ToList();
        _files.Reset(pending.Select(static change => new KeyValuePair<string, string>(change.Path, change.NewText)));
        _sent.Clear();
        Model.Update([.. pending.Select(static change => (UiBlock)new FileEditBlock
        {
            Id = "change:" + change.Path,
            CallId = "change:" + change.Path,
            Path = change.Path,
            OldText = change.Kind == ChangeKind.Added ? null : change.OldText ?? string.Empty,
            NewText = change.NewText,
        })]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ((INotifyCollectionChanged)Model.Entries).CollectionChanged -= OnEntriesChanged;
        foreach (var entry in _watched)
        {
            entry.PropertyChanged -= OnEntryChanged;
        }

        _watched.Clear();
        Model.Dispose();
    }

    private void SetNotice(string? text)
    {
        Notice = text;
        NoticeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var entry in Model.Entries)
        {
            if (_watched.Add(entry))
            {
                entry.PropertyChanged += OnEntryChanged;
            }
        }
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || sender is not ChangeEntry entry || !string.Equals(e.PropertyName, nameof(ChangeEntry.Status), StringComparison.Ordinal))
        {
            return;
        }

        var status = entry.Status;
        if (status is not (ChangeStatus.Kept or ChangeStatus.Undone))
        {
            return;
        }

        if (_sent.TryGetValue(entry.Path, out var last) && last == status)
        {
            return;
        }

        _sent[entry.Path] = status;
        _ = DecideAsync(entry.Path, status == ChangeStatus.Kept);
    }

    private async Task DecideAsync(string path, bool accept)
    {
        try
        {
            var result = await _api.DecideChangeAsync(_sessionId, path, accept, CancellationToken.None).ConfigureAwait(true);
            SetNotice(result.Applied || accept
                ? null
                : result.Reason ?? "The decision was recorded; no workspace folder is configured, so no file was written.");
        }
        catch (ApiProblemException problem) when (problem.Kind == ApiProblemKind.Conflict)
        {
            SetNotice("The file changed since the agent wrote it, so nothing was overwritten.");
            await RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            SetNotice(WebFailureText.Describe(fault));
            await RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }
}
