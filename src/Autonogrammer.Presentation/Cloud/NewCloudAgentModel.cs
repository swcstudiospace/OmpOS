using System.Globalization;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cloud;

/// <summary>
/// The new cloud agent dialog: a name, one or more repositories, branches, an environment, secret names (never values), an idle time and
/// a model. Creating an agent starts billable compute, so Create stays disabled until every entry is valid AND the user ticked the cost
/// confirmation, which says in plain words what Railway bills and when idle workers sleep. The model makes no host call before then.
/// </summary>
public sealed class NewCloudAgentModel : ObservableModel
{
    private readonly ICloudAgentsHost _host;
    private readonly CloudSetup _setup;
    private readonly CloudLimits? _limits;
    private readonly List<CloudRepository> _repositories = [];
    private readonly List<string> _secretNames = [];
    private string _name = string.Empty;
    private string _prompt = string.Empty;
    private string _branch = string.Empty;
    private string _baseBranch = string.Empty;
    private string _idleMinutesText;
    private string? _environmentId;
    private string? _modelId;
    private string _repositoryText = string.Empty;
    private string _secretNameText = string.Empty;
    private string _repositoryEntryError = string.Empty;
    private string _secretEntryError = string.Empty;
    private bool _costConfirmed;
    private bool _isCreating;
    private CloudCreateResult? _result;

    /// <summary>Initializes a new instance of the <see cref="NewCloudAgentModel"/> class.</summary>
    /// <param name="host">Where the agent is created.</param>
    /// <param name="setup">What the user may choose, from <see cref="ICloudAgentsHost.GetSetupAsync"/>.</param>
    /// <param name="limits">The caps, or null when they are not known. A reached agent cap disables Create with a reason.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="setup"/> is null.</exception>
    public NewCloudAgentModel(ICloudAgentsHost host, CloudSetup setup, CloudLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(setup);

        _host = host;
        _setup = setup;
        _limits = limits;
        _idleMinutesText = setup.DefaultIdleMinutes.ToString(CultureInfo.InvariantCulture);
        _environmentId = setup.Environments.Count == 0
            ? null
            : setup.Environments.FirstOrDefault(choice => string.Equals(choice.Id, setup.DefaultEnvironmentId, StringComparison.Ordinal))?.Id
                ?? setup.Environments[0].Id;
    }

    /// <summary>Occurs after an agent was created.</summary>
    public event EventHandler<CloudAgentCreatedEventArgs>? Created;

    /// <summary>Occurs when the user cancels the dialog. The owner closes it.</summary>
    public event EventHandler? CancelRequested;

    /// <summary>Gets the environments to choose from. Empty when the host has one fixed environment.</summary>
    public IReadOnlyList<CloudChoice> Environments => _setup.Environments;

    /// <summary>Gets the models to choose from. Empty when the host decides.</summary>
    public IReadOnlyList<CloudChoice> Models => _setup.Models;

    /// <summary>Gets whether there is an environment to choose.</summary>
    public bool HasEnvironments => _setup.Environments.Count > 0;

    /// <summary>Gets whether there is a model to choose.</summary>
    public bool HasModels => _setup.Models.Count > 0;

    /// <summary>Gets the repositories added so far, in order. The first is the primary one.</summary>
    public IReadOnlyList<CloudRepository> Repositories => _repositories;

    /// <summary>Gets the secret names added so far. Only names exist here: the dialog has no place for a value.</summary>
    public IReadOnlyList<string> SecretNames => _secretNames;

    /// <summary>Gets the stored secret names the service knows and that are not added yet.</summary>
    public IReadOnlyList<string> SuggestedSecretNames =>
        _setup.KnownSecretNames.Where(known => !_secretNames.Contains(known, StringComparer.Ordinal)).ToArray();

    /// <summary>Gets or sets the agent name.</summary>
    public string Name
    {
        get => _name;
        set => Edit(ref _name, value, nameof(Name), nameof(NameError));
    }

    /// <summary>Gets or sets the first task for the agent. Optional.</summary>
    public string Prompt
    {
        get => _prompt;
        set => Edit(ref _prompt, value, nameof(Prompt), nameof(PromptError));
    }

    /// <summary>Gets or sets the branch the agent commits to. Blank lets the service name one.</summary>
    public string Branch
    {
        get => _branch;
        set => Edit(ref _branch, value, nameof(Branch), nameof(BranchError));
    }

    /// <summary>Gets or sets the branch the agent starts from. Blank uses each repository's default branch.</summary>
    public string BaseBranch
    {
        get => _baseBranch;
        set => Edit(ref _baseBranch, value, nameof(BaseBranch), nameof(BaseBranchError));
    }

    /// <summary>Gets or sets the idle time as typed, in minutes. Changing it withdraws the cost confirmation, because the confirmation names this time.</summary>
    public string IdleMinutesText
    {
        get => _idleMinutesText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_idleMinutesText, next, StringComparison.Ordinal))
            {
                return;
            }

            _idleMinutesText = next;
            _costConfirmed = false;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(CostConfirmed));
            NotifyPropertyChanged(nameof(IdleError));
            NotifyPropertyChanged(nameof(CostNotice));
            Touch();
        }
    }

    /// <summary>Gets or sets the identifier of the chosen environment, or null for the host's default.</summary>
    public string? EnvironmentId
    {
        get => _environmentId;
        set
        {
            if (SetProperty(ref _environmentId, value))
            {
                Touch();
            }
        }
    }

    /// <summary>Gets or sets the identifier of the chosen model, or null for the host's default.</summary>
    public string? ModelId
    {
        get => _modelId;
        set
        {
            if (SetProperty(ref _modelId, value))
            {
                Touch();
            }
        }
    }

    /// <summary>Gets or sets the repository address being typed. <see cref="AddRepository"/> adds it.</summary>
    public string RepositoryText
    {
        get => _repositoryText;
        set
        {
            var next = value ?? string.Empty;
            if (SetProperty(ref _repositoryText, next))
            {
                _repositoryEntryError = string.Empty;
                NotifyPropertyChanged(nameof(RepositoryEntryError));
                NotifyPropertyChanged(nameof(CanAddRepository));
            }
        }
    }

    /// <summary>Gets or sets the secret name being typed. <see cref="AddSecretName"/> adds it.</summary>
    public string SecretNameText
    {
        get => _secretNameText;
        set
        {
            var next = value ?? string.Empty;
            if (SetProperty(ref _secretNameText, next))
            {
                _secretEntryError = string.Empty;
                NotifyPropertyChanged(nameof(SecretEntryError));
                NotifyPropertyChanged(nameof(CanAddSecretName));
            }
        }
    }

    /// <summary>Gets or sets whether the user confirmed the cost. Create stays disabled until it is true.</summary>
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

    /// <summary>Gets the sentence the cost confirmation shows, with the idle time the user entered (or the default while it is not valid).</summary>
    public string CostNotice => CloudCopy.CostNotice(EffectiveIdleMinutes());

    /// <summary>Gets the error of the name when it is too long or has control characters. A missing name is explained by <see cref="WhyDisabled"/>.</summary>
    public string NameError => _name.Trim().Length == 0 ? string.Empty : CloudInputValidator.ValidateName(_name) ?? string.Empty;

    /// <summary>Gets the error of the repository entry after an add was refused, or an empty string.</summary>
    public string RepositoryEntryError => _repositoryEntryError;

    /// <summary>Gets the error of the branch, or an empty string.</summary>
    public string BranchError => CloudInputValidator.ValidateBranch(_branch) ?? string.Empty;

    /// <summary>Gets the error of the base branch, or an empty string.</summary>
    public string BaseBranchError => CloudInputValidator.ValidateBranch(_baseBranch) ?? string.Empty;

    /// <summary>Gets the error of the idle time, or an empty string.</summary>
    public string IdleError => CloudInputValidator.ValidateIdleMinutes(_idleMinutesText, _setup.MaxIdleMinutes, out _) ?? string.Empty;

    /// <summary>Gets the error of the secret name entry after an add was refused, or an empty string. It never repeats what was typed.</summary>
    public string SecretEntryError => _secretEntryError;

    /// <summary>Gets the error of the first task, or an empty string.</summary>
    public string PromptError => CloudInputValidator.ValidatePrompt(_prompt) ?? string.Empty;

    /// <summary>Gets whether Add repository is enabled: something is typed and the list is not full.</summary>
    public bool CanAddRepository => !string.IsNullOrWhiteSpace(_repositoryText) && _repositories.Count < CloudInputValidator.MaxRepositories;

    /// <summary>Gets whether Add secret is enabled: something is typed and the list is not full.</summary>
    public bool CanAddSecretName => !string.IsNullOrWhiteSpace(_secretNameText) && _secretNames.Count < CloudInputValidator.MaxSecretNames;

    /// <summary>Gets whether every entry is valid. The cost confirmation is a separate condition of <see cref="CanCreate"/>.</summary>
    public bool IsValid => Problem() is null;

    /// <summary>Gets whether Create is enabled: the entries are valid, the cost is confirmed, no create is running and no agent was created yet.</summary>
    public bool CanCreate => !_isCreating && _result is not { IsCreated: true } && IsValid && _costConfirmed;

    /// <summary>Gets the one reason Create is disabled, in words, or an empty string when it is enabled.</summary>
    public string WhyDisabled => _result is { IsCreated: true } ? string.Empty : Problem() ?? (_costConfirmed ? string.Empty : CloudCopy.CostNotConfirmed);

    /// <summary>Gets whether a create is running.</summary>
    public bool IsCreating => _isCreating;

    /// <summary>Gets how the latest attempt ended, or null before the first attempt and after any edit.</summary>
    public CloudCreateResult? Result => _result;

    /// <summary>Adds the typed repository when it is valid, not a duplicate and the list has room. A refused entry sets <see cref="RepositoryEntryError"/> and is kept for editing.</summary>
    /// <returns>True when the repository was added.</returns>
    public bool AddRepository()
    {
        var error = CloudInputValidator.ValidateRepositoryAddress(_repositoryText, out var repository);
        if (error is null && repository is not null)
        {
            if (_repositories.Count >= CloudInputValidator.MaxRepositories)
            {
                error = CloudCopy.TooManyRepositories(CloudInputValidator.MaxRepositories);
            }
            else if (_repositories.Any(existing => string.Equals(CloudInputValidator.KeyOf(existing.Address), CloudInputValidator.KeyOf(repository.Address), StringComparison.Ordinal)))
            {
                error = CloudCopy.RepositoryDuplicate;
            }
        }

        if (error is not null || repository is null)
        {
            _repositoryEntryError = error ?? CloudCopy.RepositoryInvalid;
            NotifyPropertyChanged(nameof(RepositoryEntryError));
            return false;
        }

        _repositories.Add(repository);
        _repositoryText = string.Empty;
        _repositoryEntryError = string.Empty;
        NotifyPropertyChanged(nameof(RepositoryText));
        NotifyPropertyChanged(nameof(RepositoryEntryError));
        NotifyPropertyChanged(nameof(Repositories));
        NotifyPropertyChanged(nameof(CanAddRepository));
        Touch();
        return true;
    }

    /// <summary>Removes a repository from the list.</summary>
    /// <param name="address">The address of a listed repository.</param>
    /// <returns>True when a repository was removed.</returns>
    public bool RemoveRepository(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var index = _repositories.FindIndex(existing => string.Equals(existing.Address, address, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _repositories.RemoveAt(index);
        NotifyPropertyChanged(nameof(Repositories));
        NotifyPropertyChanged(nameof(CanAddRepository));
        Touch();
        return true;
    }

    /// <summary>
    /// Adds the typed secret name when it is a valid name and not a duplicate. A refused entry sets <see cref="SecretEntryError"/> without
    /// repeating it, and an entry that is not a valid name is cleared as well: it may be a pasted value, which must not stay on screen.
    /// </summary>
    /// <returns>True when the name was added.</returns>
    public bool AddSecretName()
    {
        var error = CloudInputValidator.ValidateSecretName(_secretNameText);
        var notAName = error is not null;
        var name = _secretNameText.Trim();
        if (error is null && _secretNames.Count >= CloudInputValidator.MaxSecretNames)
        {
            error = CloudCopy.TooManySecretNames(CloudInputValidator.MaxSecretNames);
        }
        else if (error is null && _secretNames.Contains(name, StringComparer.Ordinal))
        {
            error = CloudCopy.SecretNameDuplicate;
        }

        if (error is not null)
        {
            if (notAName)
            {
                _secretNameText = string.Empty;
                NotifyPropertyChanged(nameof(SecretNameText));
                NotifyPropertyChanged(nameof(CanAddSecretName));
            }

            _secretEntryError = error;
            NotifyPropertyChanged(nameof(SecretEntryError));
            return false;
        }

        _secretNames.Add(name);
        _secretNameText = string.Empty;
        _secretEntryError = string.Empty;
        NotifySecretsChanged();
        return true;
    }

    /// <summary>Adds one of <see cref="SuggestedSecretNames"/>.</summary>
    /// <param name="name">A stored secret name the service listed.</param>
    /// <returns>True when the name was added.</returns>
    public bool AddKnownSecretName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!SuggestedSecretNames.Contains(name, StringComparer.Ordinal) || _secretNames.Count >= CloudInputValidator.MaxSecretNames)
        {
            return false;
        }

        _secretNames.Add(name);
        NotifySecretsChanged();
        return true;
    }

    /// <summary>Removes a secret name from the list.</summary>
    /// <param name="name">The listed name.</param>
    /// <returns>True when a name was removed.</returns>
    public bool RemoveSecretName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var index = _secretNames.FindIndex(existing => string.Equals(existing, name, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _secretNames.RemoveAt(index);
        NotifySecretsChanged();
        return true;
    }

    /// <summary>
    /// Creates the agent. Without valid entries and a confirmed cost no request is made and the result is <see cref="CloudCreateOutcome.NotReady"/>.
    /// A refusal for a cap or the quota, a rejection and a failure keep the dialog open with their message so the user can change an entry
    /// and try again. This method does not throw an expected failure.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How the attempt ended.</returns>
    public async Task<CloudCreateResult> CreateAsync(CancellationToken cancellationToken)
    {
        if (_result is { IsCreated: true } done)
        {
            return done;
        }

        if (_isCreating)
        {
            return _result ?? new CloudCreateResult(CloudCreateOutcome.NotReady, CloudCopy.CreateInProgress);
        }

        if (!CanCreate)
        {
            var notReady = new CloudCreateResult(CloudCreateOutcome.NotReady, WhyDisabled.Length > 0 ? WhyDisabled : CloudCopy.CreateNotReady);
            SetResult(notReady);
            return notReady;
        }

        var request = ToRequest();
        _isCreating = true;
        SetResult(null);
        NotifyStateChanged();
        CloudCreateResult result;
        try
        {
            var agent = await _host.CreateAsync(request, cancellationToken).ConfigureAwait(true);
            result = new CloudCreateResult(CloudCreateOutcome.Created, CloudCopy.Created(PanelText.Plain(agent.Name, 120)), agent);
        }
        catch (OperationCanceledException)
        {
            result = new CloudCreateResult(CloudCreateOutcome.Failed, CloudCopy.CreateCancelled);
        }
        catch (Exception fault) when (CloudFailures.IsHandled(fault))
        {
            result = Describe(fault);
        }
        finally
        {
            _isCreating = false;
        }

        SetResult(result);
        NotifyStateChanged();
        if (result.Agent is { } created)
        {
            Created?.Invoke(this, new CloudAgentCreatedEventArgs(created));
        }

        return result;
    }

    /// <summary>Asks the owner to close the dialog without creating anything. It does nothing while a create is running.</summary>
    public void Cancel()
    {
        if (!_isCreating)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static CloudCreateResult Describe(Exception fault)
    {
        var kind = CloudFailures.KindOf(fault);
        return kind switch
        {
            CloudFailureKind.LimitReached => new CloudCreateResult(CloudCreateOutcome.LimitReached, CloudFailures.MessageOf(fault, CloudCopy.LimitReached)),
            CloudFailureKind.QuotaExceeded => new CloudCreateResult(CloudCreateOutcome.QuotaExceeded, CloudFailures.MessageOf(fault, CloudCopy.QuotaExceeded)),
            CloudFailureKind.Rejected or CloudFailureKind.Conflict or CloudFailureKind.NotFound => new CloudCreateResult(CloudCreateOutcome.Rejected, CloudFailures.MessageOf(fault, CloudCopy.CreateRejected)),
            CloudFailureKind.NotConfigured => new CloudCreateResult(CloudCreateOutcome.Failed, CloudFailures.MessageOf(fault, CloudCopy.NotConfiguredShort)),
            CloudFailureKind.Unauthorized => new CloudCreateResult(CloudCreateOutcome.Failed, CloudFailures.MessageOf(fault, CloudCopy.SignInNeeded)),
            _ => new CloudCreateResult(CloudCreateOutcome.Failed, CloudFailures.MessageOf(fault, CloudCopy.CreateFailed)),
        };
    }

    private CloudAgentRequest ToRequest()
    {
        _ = CloudInputValidator.ValidateIdleMinutes(_idleMinutesText, _setup.MaxIdleMinutes, out var minutes);
        return new CloudAgentRequest
        {
            Name = _name.Trim(),
            Repositories = [.. _repositories],
            Branch = Blank(_branch),
            BaseBranch = Blank(_baseBranch),
            EnvironmentId = HasEnvironments ? _environmentId : null,
            SecretNames = [.. _secretNames],
            IdleMinutes = minutes,
            Model = HasModels ? _modelId : null,
            Prompt = Blank(_prompt),
        };
    }

    private static string? Blank(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private int EffectiveIdleMinutes() =>
        CloudInputValidator.ValidateIdleMinutes(_idleMinutesText, _setup.MaxIdleMinutes, out var minutes) is null ? minutes : _setup.DefaultIdleMinutes;

    // The first thing that keeps Create disabled, or null when the entries are valid.
    private string? Problem()
    {
        if (_limits is { AgentCapReached: true, MaxAgents: { } max })
        {
            return CloudCopy.AgentCapReached(max);
        }

        if (CloudInputValidator.ValidateName(_name) is { } nameError)
        {
            return nameError;
        }

        if (_repositories.Count == 0)
        {
            return CloudCopy.RepositoryNeeded;
        }

        return CloudInputValidator.ValidateBranch(_branch)
            ?? CloudInputValidator.ValidateBranch(_baseBranch)
            ?? CloudInputValidator.ValidateIdleMinutes(_idleMinutesText, _setup.MaxIdleMinutes, out _)
            ?? CloudInputValidator.ValidatePrompt(_prompt);
    }

    private void Edit(ref string target, string? value, string property, string errorProperty)
    {
        if (SetProperty(ref target, value ?? string.Empty, property))
        {
            NotifyPropertyChanged(errorProperty);
            Touch();
        }
    }

    private void NotifySecretsChanged()
    {
        NotifyPropertyChanged(nameof(SecretNames));
        NotifyPropertyChanged(nameof(SuggestedSecretNames));
        NotifyPropertyChanged(nameof(SecretNameText));
        NotifyPropertyChanged(nameof(SecretEntryError));
        NotifyPropertyChanged(nameof(CanAddSecretName));
        Touch();
    }

    // An edit makes the previous result stale; the readiness properties are recomputed from the entries.
    private void Touch()
    {
        if (!_isCreating && _result is { IsCreated: false })
        {
            _result = null;
            NotifyPropertyChanged(nameof(Result));
        }

        NotifyStateChanged();
    }

    private void SetResult(CloudCreateResult? result)
    {
        _result = result;
        NotifyPropertyChanged(nameof(Result));
    }

    private void NotifyStateChanged()
    {
        NotifyPropertyChanged(nameof(IsValid));
        NotifyPropertyChanged(nameof(CanCreate));
        NotifyPropertyChanged(nameof(WhyDisabled));
        NotifyPropertyChanged(nameof(IsCreating));
    }
}
