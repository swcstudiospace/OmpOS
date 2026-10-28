using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Cloud;

/// <summary>
/// The cost-confirmed hand-off dialog behind <c>/in-cloud</c> and the <c>&amp;</c> prefix: the typed task (read-only), one repository address and the
/// cost confirmation. Handing off starts billable compute, so <see cref="CanStart"/> is true only for a valid repository, a ticked confirmation and a
/// task; the model makes no host call before then. There is no field for a secret value.
/// </summary>
public sealed class CloudHandoffModel : ObservableModel
{
    private readonly ICloudHandoff _handoff;
    private readonly CloudSetup _setup;
    private string _repositoryText;
    private string _status = string.Empty;
    private bool _hasError;
    private bool _costConfirmed;
    private bool _isBusy;
    private CloudHandoffResult? _result;

    /// <summary>Initializes a new instance of the <see cref="CloudHandoffModel"/> class.</summary>
    /// <param name="handoff">Creates the agent.</param>
    /// <param name="setup">The setup of the host; its default idle time is named in the cost notice and sent with the request.</param>
    /// <param name="task">The task the user typed.</param>
    /// <param name="suggestedRepository">A repository address to start with, for example the remote of the session, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handoff"/>, <paramref name="setup"/> or <paramref name="task"/> is null.</exception>
    public CloudHandoffModel(ICloudHandoff handoff, CloudSetup setup, string task, string? suggestedRepository = null)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(task);

        _handoff = handoff;
        _setup = setup;
        Task = task;
        _repositoryText = suggestedRepository?.Trim() ?? string.Empty;
    }

    /// <summary>Occurs after the agent was started. The owner closes the dialog.</summary>
    public event EventHandler<CloudAgentCreatedEventArgs>? Started;

    /// <summary>Occurs when the user cancels the dialog. The owner closes it.</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Gets the task the user typed. It is shown as text and cannot be edited here.</summary>
    public string Task { get; }

    /// <summary>Gets or sets the repository address the agent works in. Editing it never withdraws the cost confirmation.</summary>
    public string RepositoryText
    {
        get => _repositoryText;
        set
        {
            if (SetProperty(ref _repositoryText, value ?? string.Empty))
            {
                Touch();
                NotifyPropertyChanged(nameof(RepositoryError));
            }
        }
    }

    /// <summary>Gets the sentence of what is wrong with the repository address. Empty while nothing was typed or the address is valid; it never repeats the entry.</summary>
    public string RepositoryError => _repositoryText.Trim().Length == 0 ? string.Empty : CloudInputValidator.ValidateRepositoryUrl(_repositoryText, out _) ?? string.Empty;

    /// <summary>Gets the sentence that says what is billed and when idle workers sleep.</summary>
    public string CostNotice => CloudCopy.CostNotice(_setup.DefaultIdleMinutes);

    /// <summary>Gets or sets whether the user confirmed the cost. The hand-off cannot start before it is true.</summary>
    public bool CostConfirmed
    {
        get => _costConfirmed;
        set
        {
            if (SetProperty(ref _costConfirmed, value))
            {
                Touch();
            }
        }
    }

    /// <summary>Gets the sentence about the latest attempt or the running hand-off. Empty before the first attempt.</summary>
    public string Status => _status;

    /// <summary>Gets whether <see cref="Status"/> is a problem the user has to act on.</summary>
    public bool HasError => _hasError;

    /// <summary>Gets whether a hand-off is running.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets the result of the latest attempt, or null before the first one and after an edit made it stale.</summary>
    public CloudHandoffResult? Result => _result;

    /// <summary>Gets whether the hand-off can start: a valid repository, a confirmed cost, a task and no hand-off running or done.</summary>
    public bool CanStart => WhyDisabled.Length == 0;

    /// <summary>Gets the sentence that says what is missing before the hand-off can start. Empty when <see cref="CanStart"/> is true.</summary>
    public string WhyDisabled
    {
        get
        {
            if (_isBusy)
            {
                return CloudCopy.CreateInProgress;
            }

            if (_result is { Outcome: CloudHandoffOutcome.Started })
            {
                return _result.Message;
            }

            if (Task.Trim().Length == 0)
            {
                return CloudCopy.HandoffNeedsPrompt;
            }

            if (_repositoryText.Trim().Length == 0)
            {
                return CloudCopy.RepositoryRequired;
            }

            if (RepositoryError.Length > 0)
            {
                return RepositoryError;
            }

            return _costConfirmed ? string.Empty : CloudCopy.CostNotConfirmed;
        }
    }

    /// <summary>
    /// Hands the task off. Without a valid repository, a task and the cost confirmation no host call is made and the result says what is missing.
    /// A refusal for a cap or the quota, a rejection and a failure keep the dialog open with their message. This method does not throw an expected failure.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How the attempt ended.</returns>
    public async Task<CloudHandoffResult> StartAsync(CancellationToken cancellationToken)
    {
        if (_result is { Outcome: CloudHandoffOutcome.Started } done)
        {
            return done;
        }

        if (_isBusy)
        {
            return new CloudHandoffResult(CloudHandoffOutcome.NeedsInput, CloudCopy.CreateInProgress);
        }

        if (!CanStart)
        {
            var needsConfirmation = Task.Trim().Length > 0 && RepositoryReady() && !_costConfirmed;
            var notReady = new CloudHandoffResult(
                needsConfirmation ? CloudHandoffOutcome.NeedsCostConfirmation : CloudHandoffOutcome.NeedsInput,
                WhyDisabled);
            Settle(notReady);
            return notReady;
        }

        _ = CloudInputValidator.ValidateRepositoryUrl(_repositoryText, out var repository);
        var request = new CloudHandoffRequest
        {
            Prompt = Task,
            Repositories = [repository!],
            CostConfirmed = _costConfirmed,
            IdleMinutes = _setup.DefaultIdleMinutes,
        };

        _isBusy = true;
        _result = null;
        _status = CloudCopy.HandoffInProgress;
        _hasError = false;
        NotifyStateChanged();

        CloudHandoffResult result;
        try
        {
            result = await _handoff.StartAsync(request, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _isBusy = false;
        }

        Settle(result);
        if (result is { Outcome: CloudHandoffOutcome.Started, Agent: { } agent })
        {
            Started?.Invoke(this, new CloudAgentCreatedEventArgs(agent));
        }

        return result;
    }

    /// <summary>Asks the owner to close the dialog without handing anything off. It does nothing while a hand-off is running.</summary>
    public void Cancel()
    {
        if (!_isBusy)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool RepositoryReady() => _repositoryText.Trim().Length > 0 && RepositoryError.Length == 0;

    // An edit makes a refusal stale: its sentence goes away and the readiness properties are recomputed.
    private void Touch()
    {
        if (!_isBusy && _result is { Outcome: not CloudHandoffOutcome.Started })
        {
            _result = null;
            _status = string.Empty;
            _hasError = false;
            NotifyPropertyChanged(nameof(Result));
            NotifyPropertyChanged(nameof(Status));
            NotifyPropertyChanged(nameof(HasError));
        }

        NotifyStateChanged();
    }

    private void Settle(CloudHandoffResult result)
    {
        _result = result;
        _status = result.Message;
        _hasError = result.Outcome != CloudHandoffOutcome.Started;
        NotifyPropertyChanged(nameof(Result));
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(HasError));
        NotifyPropertyChanged(nameof(IsBusy));
        NotifyPropertyChanged(nameof(CanStart));
        NotifyPropertyChanged(nameof(WhyDisabled));
    }
}
