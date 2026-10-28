using System.Globalization;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Automations;

/// <summary>
/// The form that creates or edits one hosted automation: a name, a prompt, a schedule and a webhook trigger, a target (a local session or
/// a cloud agent with repositories and secret names) and the limits of a run. Typed entries are checked on the client before any call
/// reaches the <see cref="IAutomationsManager"/>; a message never repeats what was typed.
/// </summary>
/// <remarks>
/// <para>
/// A field message shows once the user edited that field or tried to save, so a new form does not open full of errors;
/// <see cref="CanSave"/> and <see cref="WhyDisabled"/> always reflect every rule. The webhook secret a save or a rotation returns lives
/// only in <see cref="RevealedSecret"/> until <see cref="AcknowledgeSecret"/> clears it; no other property, message or
/// <see cref="object.ToString"/> output holds it.
/// </para>
/// <para>
/// Expected failures (<see cref="AutomationsSourceException"/>) become <see cref="SaveFailure"/> or <see cref="CronPreviewError"/> and are
/// never thrown. Cancelling throws <see cref="OperationCanceledException"/> and leaves the entries as they were. After the first
/// successful save the form edits the saved automation, so a second save updates it instead of creating another.
/// </para>
/// </remarks>
public sealed class AutomationEditorModel : ObservableModel
{
    /// <summary>The name of the pull request action that opens pull requests.</summary>
    public const string PullRequestCreateTool = "pull_request_create";

    /// <summary>The name of the pull request action that comments on pull requests.</summary>
    public const string PullRequestCommentTool = "pull_request_comment";

    /// <summary>The name of the pull request action that requests reviewers.</summary>
    public const string PullRequestRequestReviewersTool = "pull_request_request_reviewers";

    /// <summary>The longest name, in characters.</summary>
    public const int NameMaxLength = 100;

    /// <summary>The longest prompt, in characters.</summary>
    public const int PromptMaxLength = 20000;

    /// <summary>The most repositories a cloud target may have.</summary>
    public const int MaxRepositories = 16;

    /// <summary>How many next runs a cron preview asks for.</summary>
    public const int PreviewCount = 5;

    private const string CronKind = "cron";
    private const string WebhookKind = "webhook";
    private const string GenericFlavour = "generic";
    private const string GithubFlavour = "github";
    private const string CloudKind = "cloud";
    private const string HttpsPrefix = "https://";
    private const int SecretNameMaxLength = 64;
    private const int FilterMaxLength = 64;
    private const int BranchMaxLength = 255;
    private const int AddressMaxLength = 2048;

    private static readonly char[] LineSeparators = ['\r', '\n'];
    private static readonly char[] WhitespaceSeparators = [' ', '\t'];

    private static readonly string[] TargetFields =
    [
        AutomationsCopy.FieldTarget,
        AutomationsCopy.FieldRepositories,
        AutomationsCopy.FieldSecrets,
    ];

    private static readonly string[] FieldOrder =
    [
        AutomationsCopy.FieldName,
        AutomationsCopy.FieldPrompt,
        AutomationsCopy.FieldTriggers,
        AutomationsCopy.FieldCron,
        AutomationsCopy.FieldWebhook,
        AutomationsCopy.FieldTarget,
        AutomationsCopy.FieldRepositories,
        AutomationsCopy.FieldSecrets,
        AutomationsCopy.FieldLimits,
    ];

    private readonly IAutomationsManager _manager;
    private readonly string[] _supportedTargetKinds;
    private readonly List<AutomationTriggerDraft> _extraTriggers = [];
    private readonly List<string> _extraTools = [];
    private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
    private HostedAutomationEntry? _existing;
    private Dictionary<string, string> _allErrors = new(StringComparer.Ordinal);
    private Dictionary<string, string> _errors = new(StringComparer.Ordinal);
    private string[] _previewLines = [];
    private string _previewError = string.Empty;
    private string? _previewKey;
    private string _saveFailure = string.Empty;
    private string _revealedSecret = string.Empty;
    private HostedAutomationEntry? _savedEntry;
    private bool _isSaving;
    private bool _isRotating;
    private bool _isPreviewing;

    private string _name = string.Empty;
    private string _prompt = string.Empty;
    private bool _enabled = true;
    private string _timeZone = "UTC";
    private string _cronText = string.Empty;
    private bool _webhookEnabled;
    private string _webhookFlavour = GenericFlavour;
    private string _webhookEventsText = string.Empty;
    private string _webhookActionsText = string.Empty;
    private string _webhookBranchesText = string.Empty;
    private string _targetKind;
    private string _repositoriesText = string.Empty;
    private string _secretRefsText = string.Empty;
    private string _environmentId = string.Empty;
    private string _model = string.Empty;
    private string _mode = string.Empty;
    private bool _allowCreate = true;
    private bool _allowComment;
    private bool _allowReviewers;
    private string _maxConcurrentRunsText = "1";
    private string _maxRunsPerHourText = "12";
    private string _maxRuntimeMinutesText = "30";

    /// <summary>Creates the form.</summary>
    /// <param name="manager">Where the automation is saved and cron schedules are previewed.</param>
    /// <param name="existing">The automation to edit, or null to create a new one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> is null.</exception>
    public AutomationEditorModel(IAutomationsManager manager, HostedAutomationEntry? existing)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _manager = manager;
        _supportedTargetKinds = [.. manager.SupportedTargetKinds];
        _targetKind = _supportedTargetKinds.Length > 0 ? _supportedTargetKinds[0] : CloudKind;
        _existing = existing;
        if (existing is not null)
        {
            Load(existing);
        }

        _allErrors = Validate();
    }

    /// <summary>Occurs once after an entry, a message, a preview or the state of a save changed, and the properties were announced.</summary>
    public event EventHandler? Changed;

    /// <summary>Occurs after the automation was saved. <see cref="SavedEntry"/> and <see cref="RevealedSecret"/> are set.</summary>
    public event EventHandler? Saved;

    /// <summary>Gets whether the form edits a saved automation: it was opened on one, or it was saved once.</summary>
    public bool IsEdit => _existing is not null;

    /// <summary>Gets the target kinds the application can run, in order of preference.</summary>
    public IReadOnlyList<string> SupportedTargetKinds => _supportedTargetKinds;

    /// <summary>Gets or sets the name. Surrounding spaces are not part of it.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (Edit(ref _name, value ?? string.Empty, nameof(Name), AutomationsCopy.FieldName))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the prompt each run is given.</summary>
    public string Prompt
    {
        get => _prompt;
        set
        {
            if (Edit(ref _prompt, value ?? string.Empty, nameof(Prompt), AutomationsCopy.FieldPrompt))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets whether the automation starts runs.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (Edit(ref _enabled, value, nameof(Enabled)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the IANA time zone the schedule is read in. An empty entry means <c>UTC</c>.</summary>
    public string TimeZone
    {
        get => _timeZone;
        set
        {
            if (Edit(ref _timeZone, value ?? string.Empty, nameof(TimeZone), AutomationsCopy.FieldCron))
            {
                ClearPreview();
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the cron expression of the schedule. An empty entry means the automation has no schedule trigger.</summary>
    public string CronText
    {
        get => _cronText;
        set
        {
            if (Edit(ref _cronText, value ?? string.Empty, nameof(CronText), AutomationsCopy.FieldCron, AutomationsCopy.FieldTriggers))
            {
                ClearPreview();
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets whether a signed webhook starts the automation.</summary>
    public bool WebhookEnabled
    {
        get => _webhookEnabled;
        set
        {
            if (Edit(ref _webhookEnabled, value, nameof(WebhookEnabled), AutomationsCopy.FieldWebhook, AutomationsCopy.FieldTriggers))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the webhook type: <c>generic</c> or <c>github</c>.</summary>
    public string WebhookFlavour
    {
        get => _webhookFlavour;
        set
        {
            if (Edit(ref _webhookFlavour, value ?? string.Empty, nameof(WebhookFlavour), AutomationsCopy.FieldWebhook))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the GitHub event names, comma separated. Used by a <c>github</c> webhook only.</summary>
    public string WebhookEventsText
    {
        get => _webhookEventsText;
        set
        {
            if (Edit(ref _webhookEventsText, value ?? string.Empty, nameof(WebhookEventsText), AutomationsCopy.FieldWebhook))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the GitHub action names, comma separated. Used by a <c>github</c> webhook only.</summary>
    public string WebhookActionsText
    {
        get => _webhookActionsText;
        set
        {
            if (Edit(ref _webhookActionsText, value ?? string.Empty, nameof(WebhookActionsText), AutomationsCopy.FieldWebhook))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the branch names or globs, comma separated. Used by a <c>github</c> webhook only.</summary>
    public string WebhookBranchesText
    {
        get => _webhookBranchesText;
        set
        {
            if (Edit(ref _webhookBranchesText, value ?? string.Empty, nameof(WebhookBranchesText), AutomationsCopy.FieldWebhook))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets where runs happen: <c>local</c> or <c>cloud</c>.</summary>
    public string TargetKind
    {
        get => _targetKind;
        set
        {
            if (Edit(ref _targetKind, value ?? string.Empty, nameof(TargetKind), TargetFields))
            {
                NotifyPropertyChanged(nameof(IsCloudTarget));
                Refresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the repositories of a cloud target, one per line: an https address, then optionally a branch and a base branch separated
    /// by spaces. A dash stands for no branch.
    /// </summary>
    public string RepositoriesText
    {
        get => _repositoriesText;
        set
        {
            if (Edit(ref _repositoriesText, value ?? string.Empty, nameof(RepositoriesText), AutomationsCopy.FieldRepositories))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the names, never the values, of the stored secrets a cloud agent may read, comma separated.</summary>
    public string SecretRefsText
    {
        get => _secretRefsText;
        set
        {
            if (Edit(ref _secretRefsText, value ?? string.Empty, nameof(SecretRefsText), AutomationsCopy.FieldSecrets))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the cloud environment. An empty entry means the default of the host.</summary>
    public string EnvironmentId
    {
        get => _environmentId;
        set
        {
            if (Edit(ref _environmentId, value ?? string.Empty, nameof(EnvironmentId)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the model. An empty entry means the default of the host.</summary>
    public string Model
    {
        get => _model;
        set
        {
            if (Edit(ref _model, value ?? string.Empty, nameof(Model)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets the agent mode. An empty entry means the default of the host.</summary>
    public string Mode
    {
        get => _mode;
        set
        {
            if (Edit(ref _mode, value ?? string.Empty, nameof(Mode)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets whether a run may open pull requests.</summary>
    public bool AllowPullRequestCreate
    {
        get => _allowCreate;
        set
        {
            if (Edit(ref _allowCreate, value, nameof(AllowPullRequestCreate)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets whether a run may comment on pull requests.</summary>
    public bool AllowPullRequestComment
    {
        get => _allowComment;
        set
        {
            if (Edit(ref _allowComment, value, nameof(AllowPullRequestComment)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets whether a run may request reviewers.</summary>
    public bool AllowRequestReviewers
    {
        get => _allowReviewers;
        set
        {
            if (Edit(ref _allowReviewers, value, nameof(AllowRequestReviewers)))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets how many runs may be in progress at once, as typed. A whole number from 1 to 4.</summary>
    public string MaxConcurrentRunsText
    {
        get => _maxConcurrentRunsText;
        set
        {
            if (Edit(ref _maxConcurrentRunsText, value ?? string.Empty, nameof(MaxConcurrentRunsText), AutomationsCopy.FieldLimits))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets how many runs may start within one hour, as typed. A whole number from 1 to 60.</summary>
    public string MaxRunsPerHourText
    {
        get => _maxRunsPerHourText;
        set
        {
            if (Edit(ref _maxRunsPerHourText, value ?? string.Empty, nameof(MaxRunsPerHourText), AutomationsCopy.FieldLimits))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets or sets how long one run may take, in minutes, as typed. A whole number from 1 to 360.</summary>
    public string MaxRuntimeMinutesText
    {
        get => _maxRuntimeMinutesText;
        set
        {
            if (Edit(ref _maxRuntimeMinutesText, value ?? string.Empty, nameof(MaxRuntimeMinutesText), AutomationsCopy.FieldLimits))
            {
                Refresh();
            }
        }
    }

    /// <summary>Gets whether the target is a cloud agent, so the repositories and secret names apply.</summary>
    public bool IsCloudTarget => string.Equals(_targetKind, CloudKind, StringComparison.Ordinal);

    /// <summary>Gets whether the webhook is a GitHub webhook, so its event, action and branch filters apply.</summary>
    public bool IsGithubWebhook => string.Equals(_webhookFlavour, GithubFlavour, StringComparison.Ordinal);

    /// <summary>Gets the messages of the fields the user edited or tried to save, keyed by field: name, prompt, triggers, cron, webhook, target, repositories, secrets and limits.</summary>
    public IReadOnlyDictionary<string, string> Errors => _errors;

    /// <summary>Gets the message of the name field, or an empty string.</summary>
    public string NameError => ErrorOf(AutomationsCopy.FieldName);

    /// <summary>Gets the message of the prompt field, or an empty string.</summary>
    public string PromptError => ErrorOf(AutomationsCopy.FieldPrompt);

    /// <summary>Gets the message of the trigger set, or an empty string.</summary>
    public string TriggersError => ErrorOf(AutomationsCopy.FieldTriggers);

    /// <summary>Gets the message of the schedule, or an empty string.</summary>
    public string CronError => ErrorOf(AutomationsCopy.FieldCron);

    /// <summary>Gets the message of the webhook settings, or an empty string.</summary>
    public string WebhookError => ErrorOf(AutomationsCopy.FieldWebhook);

    /// <summary>Gets the message of the target, or an empty string.</summary>
    public string TargetError => ErrorOf(AutomationsCopy.FieldTarget);

    /// <summary>Gets the message of the repositories, or an empty string.</summary>
    public string RepositoriesError => ErrorOf(AutomationsCopy.FieldRepositories);

    /// <summary>Gets the message of the secret names, or an empty string.</summary>
    public string SecretsError => ErrorOf(AutomationsCopy.FieldSecrets);

    /// <summary>Gets the message of the limits, or an empty string.</summary>
    public string LimitsError => ErrorOf(AutomationsCopy.FieldLimits);

    /// <summary>Gets whether every entry is valid and no save or rotation is running. Save is available only then.</summary>
    public bool CanSave => _allErrors.Count == 0 && !_isSaving && !_isRotating;

    /// <summary>Gets the sentence that says which fields keep Save unavailable, or an empty string when Save is available.</summary>
    public string WhyDisabled
    {
        get
        {
            if (_isSaving)
            {
                return AutomationsCopy.Saving;
            }

            if (_allErrors.Count == 0)
            {
                return string.Empty;
            }

            var fields = FieldOrder.Where(_allErrors.ContainsKey).Select(AutomationsCopy.FieldText);
            return AutomationsCopy.SaveBlockedPrefix + string.Join(", ", fields) + ".";
        }
    }

    /// <summary>Gets whether a save is running.</summary>
    public bool IsSaving => _isSaving;

    /// <summary>Gets whether a preview of the schedule is running.</summary>
    public bool IsPreviewing => _isPreviewing;

    /// <summary>Gets the reason the latest save or secret rotation failed, or an empty string when it did not.</summary>
    public string SaveFailure => _saveFailure;

    /// <summary>Gets the previewed next runs, one numbered line each, or an empty list when there is no preview.</summary>
    public IReadOnlyList<string> CronPreviewLines => _previewLines;

    /// <summary>Gets why the schedule is invalid or never runs, or an empty string when the preview is valid or absent.</summary>
    public string CronPreviewError => _previewError;

    /// <summary>
    /// Gets the webhook secret a save or a rotation just returned, or an empty string. It is shown once: <see cref="AcknowledgeSecret"/>
    /// clears it and nothing can read it again.
    /// </summary>
    public string RevealedSecret => _revealedSecret;

    /// <summary>Gets the path the webhook receives on, or an empty string when the saved automation has no webhook trigger.</summary>
    public string WebhookPath => _existing?.WebhookPath ?? string.Empty;

    /// <summary>Gets whether the saved automation has a webhook secret. The secret itself is not readable.</summary>
    public bool HasWebhookSecret => _existing?.HasWebhookSecret == true;

    /// <summary>Gets whether the webhook secret can be replaced: the automation is saved with a webhook and nothing is running.</summary>
    public bool CanRotateSecret => WebhookPath.Length > 0 && !_isSaving && !_isRotating;

    /// <summary>Gets the automation the latest successful save returned, or null before any save.</summary>
    public HostedAutomationEntry? SavedEntry => _savedEntry;

    /// <summary>Builds the draft the form describes. Call it on a valid form: an invalid limit falls back to its default and an invalid repository line is left out.</summary>
    /// <returns>The draft.</returns>
    public AutomationDraft ToDraft()
    {
        var cloud = IsCloudTarget;
        return new AutomationDraft(
            _name.Trim(),
            _prompt,
            _enabled,
            EffectiveTimeZone(),
            BuildTriggers(),
            _targetKind,
            cloud ? ParseRepositories(_repositoriesText, out _) : [],
            cloud ? SplitList(_secretRefsText).Distinct(StringComparer.Ordinal).ToArray() : [],
            _environmentId.Trim(),
            _model.Trim(),
            _mode.Trim(),
            BuildTools(),
            LimitOrDefault(_maxConcurrentRunsText, 1, 4, 1),
            LimitOrDefault(_maxRunsPerHourText, 1, 60, 12),
            LimitOrDefault(_maxRuntimeMinutesText, 1, 360, 30));
    }

    /// <summary>
    /// Previews the next runs of the schedule. An empty schedule clears the preview and makes no call. An invalid schedule shows its reason
    /// in <see cref="CronPreviewError"/>. An entry edited while the call runs makes the result stale, and it is dropped.
    /// </summary>
    /// <param name="cancellationToken">Cancels the preview.</param>
    /// <returns>A task that completes when the preview is shown.</returns>
    public async Task PreviewCronAsync(CancellationToken cancellationToken)
    {
        var cron = _cronText.Trim();
        if (cron.Length == 0)
        {
            ClearPreview();
            Refresh();
            return;
        }

        var zone = EffectiveTimeZone();
        var key = PreviewKeyOf(cron, zone);
        _isPreviewing = true;
        Refresh();

        AutomationCronPreview? preview = null;
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            preview = await _manager.PreviewCronAsync(cron, zone, PreviewCount, cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }
        catch (Exception)
        {
            _isPreviewing = false;
            Refresh();
            throw;
        }

        _isPreviewing = false;

        if (!string.Equals(key, PreviewKeyOf(_cronText.Trim(), EffectiveTimeZone()), StringComparison.Ordinal))
        {
            Refresh();
            return;
        }

        _previewKey = key;
        if (preview is null)
        {
            _previewLines = [];
            _previewError = failure ?? string.Empty;
        }
        else
        {
            _previewLines = preview.NextRuns.Select(static (at, index) => AutomationsCopy.PreviewLine(index + 1, at)).ToArray();
            _previewError = preview.Error.Length == 0 && _previewLines.Length == 0 ? AutomationsCopy.CronNeverRuns : preview.Error;
        }

        NotifyPreview();
        Refresh();
    }

    /// <summary>
    /// Saves the automation. Every rule is checked first; when an entry is invalid its message shows and the manager is not called.
    /// A saved automation sets <see cref="SavedEntry"/>, shows a newly generated webhook secret in <see cref="RevealedSecret"/> and raises
    /// <see cref="Saved"/>. A save that is already running is not started again.
    /// </summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the save ended.</returns>
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_isSaving || _isRotating)
        {
            return;
        }

        foreach (var field in FieldOrder)
        {
            _touched.Add(field);
        }

        if (Validate().Count > 0)
        {
            _saveFailure = string.Empty;
            Refresh();
            return;
        }

        _isSaving = true;
        _saveFailure = string.Empty;
        Refresh();

        AutomationSaveResult? result = null;
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            result = await _manager.SaveAsync(_existing?.Id, ToDraft(), cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }
        catch (Exception)
        {
            _isSaving = false;
            Refresh();
            throw;
        }

        _isSaving = false;

        if (result is null)
        {
            _saveFailure = failure ?? string.Empty;
            Refresh();
            return;
        }

        _existing = result.Entry;
        _savedEntry = result.Entry;
        if (!string.IsNullOrEmpty(result.WebhookSecret))
        {
            _revealedSecret = result.WebhookSecret;
        }

        NotifyPropertyChanged(nameof(SavedEntry));
        NotifyPropertyChanged(nameof(RevealedSecret));
        Refresh();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Replaces the webhook secret of the saved automation and shows the new one once in <see cref="RevealedSecret"/>. Does nothing when <see cref="CanRotateSecret"/> is false.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the secret was replaced or the request failed.</returns>
    public async Task RotateSecretAsync(CancellationToken cancellationToken)
    {
        if (!CanRotateSecret || _existing is null)
        {
            return;
        }

        var id = _existing.Id;
        _isRotating = true;
        _saveFailure = string.Empty;
        Refresh();

        string? secret = null;
        string? failure = null;
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            secret = await _manager.RotateWebhookSecretAsync(id, cancellationToken).ConfigureAwait(true);
        }
        catch (AutomationsSourceException exception)
        {
            failure = exception.Message;
        }
        catch (Exception)
        {
            _isRotating = false;
            Refresh();
            throw;
        }

        _isRotating = false;

        if (secret is null)
        {
            _saveFailure = failure ?? string.Empty;
            Refresh();
            return;
        }

        _revealedSecret = secret;
        _existing = _existing with { HasWebhookSecret = true };
        NotifyPropertyChanged(nameof(RevealedSecret));
        NotifyPropertyChanged(nameof(HasWebhookSecret));
        Refresh();
    }

    /// <summary>Clears <see cref="RevealedSecret"/>. The user confirmed the secret is stored; it cannot be shown again.</summary>
    public void AcknowledgeSecret()
    {
        if (_revealedSecret.Length == 0)
        {
            return;
        }

        _revealedSecret = string.Empty;
        NotifyPropertyChanged(nameof(RevealedSecret));
        Refresh();
    }

    private static string PreviewKeyOf(string cron, string zone) => zone + "|" + cron;

    private static string[] SplitList(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsSecretName(string name)
    {
        if (name.Length is 0 or > SecretNameMaxLength || !(IsAsciiLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        return name.All(static c => IsAsciiLetter(c) || IsAsciiDigit(c) || c == '_');
    }

    private static bool IsFilterName(string name) =>
        name.Length is > 0 and <= FilterMaxLength
        && name.All(static c => IsAsciiLetter(c) || IsAsciiDigit(c) || c is '_' or '-' or '.' or '*');

    private static bool IsBranchName(string name) =>
        name.Length is > 0 and <= BranchMaxLength && !name.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c));

    private static bool IsHttpsAddress(string address)
    {
        if (address.Length > AddressMaxLength || !address.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = address[HttpsPrefix.Length..];
        var end = rest.AsSpan().IndexOfAny('/', '?', '#');
        var authority = end < 0 ? rest : rest[..end];
        return authority.Length > 0 && !authority.Contains('@', StringComparison.Ordinal) && !address.Any(char.IsControl);
    }

    private static bool TryParseLimit(string text, int min, int max, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;

    private static int LimitOrDefault(string text, int min, int max, int fallback) =>
        TryParseLimit(text, min, max, out var value) ? value : fallback;

    private static string DashIfEmpty(string text) => text.Length == 0 ? "-" : text;

    private static string FromDash(string token) => string.Equals(token, "-", StringComparison.Ordinal) ? string.Empty : token;

    private static bool IsValidBranchToken(string token) => string.Equals(token, "-", StringComparison.Ordinal) || IsBranchName(token);

    private static List<AutomationRepositoryDraft> ParseRepositories(string text, out bool valid)
    {
        var repositories = new List<AutomationRepositoryDraft>();
        valid = true;
        foreach (var line in text.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tokens = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length is 0 or > 3 || !IsHttpsAddress(tokens[0]) || !tokens.Skip(1).All(IsValidBranchToken))
            {
                valid = false;
                continue;
            }

            var branch = tokens.Length > 1 ? FromDash(tokens[1]) : string.Empty;
            var baseBranch = tokens.Length > 2 ? FromDash(tokens[2]) : string.Empty;
            repositories.Add(new AutomationRepositoryDraft(tokens[0], branch, baseBranch));
        }

        return repositories;
    }

    private static string FormatRepository(AutomationRepositoryDraft repository)
    {
        if (repository.Branch.Length == 0 && repository.BaseBranch.Length == 0)
        {
            return repository.Url;
        }

        var text = repository.Url + " " + DashIfEmpty(repository.Branch);
        return repository.BaseBranch.Length == 0 ? text : text + " " + repository.BaseBranch;
    }

    private string EffectiveTimeZone() => _timeZone.Trim().Length == 0 ? "UTC" : _timeZone.Trim();

    private string ErrorOf(string field) => _errors.TryGetValue(field, out var message) ? message : string.Empty;

    private void Load(HostedAutomationEntry entry)
    {
        var draft = entry.Draft;
        _name = draft.Name;
        _prompt = draft.Prompt;
        _enabled = draft.Enabled;
        _timeZone = draft.TimeZone.Length == 0 ? "UTC" : draft.TimeZone;
        _targetKind = draft.TargetKind;
        _repositoriesText = string.Join('\n', draft.Repositories.Select(FormatRepository));
        _secretRefsText = string.Join(", ", draft.SecretRefs);
        _environmentId = draft.EnvironmentId;
        _model = draft.Model;
        _mode = draft.Mode;
        _maxConcurrentRunsText = draft.MaxConcurrentRuns.ToString(CultureInfo.InvariantCulture);
        _maxRunsPerHourText = draft.MaxRunsPerHour.ToString(CultureInfo.InvariantCulture);
        _maxRuntimeMinutesText = draft.MaxRuntimeMinutes.ToString(CultureInfo.InvariantCulture);

        LoadTriggers(draft);
        LoadTools(draft);
    }

    private void LoadTriggers(AutomationDraft draft)
    {
        var cronSeen = false;
        var webhookSeen = false;
        foreach (var trigger in draft.Triggers)
        {
            if (string.Equals(trigger.Kind, CronKind, StringComparison.Ordinal) && !cronSeen)
            {
                cronSeen = true;
                _cronText = trigger.Cron;
            }
            else if (string.Equals(trigger.Kind, WebhookKind, StringComparison.Ordinal) && !webhookSeen)
            {
                webhookSeen = true;
                _webhookEnabled = true;
                _webhookFlavour = trigger.Flavour.Length == 0 ? GenericFlavour : trigger.Flavour;
                _webhookEventsText = string.Join(", ", trigger.Events);
                _webhookActionsText = string.Join(", ", trigger.Actions);
                _webhookBranchesText = string.Join(", ", trigger.Branches);
            }
            else
            {
                _extraTriggers.Add(trigger);
            }
        }
    }

    private void LoadTools(AutomationDraft draft)
    {
        _allowCreate = draft.AllowedTools.Contains(PullRequestCreateTool, StringComparer.Ordinal);
        _allowComment = draft.AllowedTools.Contains(PullRequestCommentTool, StringComparer.Ordinal);
        _allowReviewers = draft.AllowedTools.Contains(PullRequestRequestReviewersTool, StringComparer.Ordinal);
        _extraTools.AddRange(draft.AllowedTools.Where(static tool =>
            !string.Equals(tool, PullRequestCreateTool, StringComparison.Ordinal)
            && !string.Equals(tool, PullRequestCommentTool, StringComparison.Ordinal)
            && !string.Equals(tool, PullRequestRequestReviewersTool, StringComparison.Ordinal)));
    }

    private List<AutomationTriggerDraft> BuildTriggers()
    {
        var triggers = new List<AutomationTriggerDraft>();
        var cron = _cronText.Trim();
        if (cron.Length > 0)
        {
            triggers.Add(new AutomationTriggerDraft(CronKind, cron, string.Empty, [], [], []));
        }

        if (_webhookEnabled)
        {
            var github = IsGithubWebhook;
            triggers.Add(new AutomationTriggerDraft(
                WebhookKind,
                string.Empty,
                _webhookFlavour,
                github ? SplitList(_webhookEventsText) : [],
                github ? SplitList(_webhookActionsText) : [],
                github ? SplitList(_webhookBranchesText) : []));
        }

        triggers.AddRange(_extraTriggers);
        return triggers;
    }

    private List<string> BuildTools()
    {
        var tools = new List<string>();
        if (_allowCreate)
        {
            tools.Add(PullRequestCreateTool);
        }

        if (_allowComment)
        {
            tools.Add(PullRequestCommentTool);
        }

        if (_allowReviewers)
        {
            tools.Add(PullRequestRequestReviewersTool);
        }

        tools.AddRange(_extraTools);
        return tools;
    }

    private Dictionary<string, string> Validate()
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        ValidateText(errors);
        ValidateTriggers(errors);
        ValidateTarget(errors);
        ValidateLimits(errors);
        return errors;
    }

    private void ValidateText(Dictionary<string, string> errors)
    {
        var name = _name.Trim();
        if (name.Length == 0)
        {
            errors[AutomationsCopy.FieldName] = AutomationsCopy.NameRequired;
        }
        else if (name.Length > NameMaxLength)
        {
            errors[AutomationsCopy.FieldName] = AutomationsCopy.NameTooLong;
        }

        if (_prompt.Trim().Length == 0)
        {
            errors[AutomationsCopy.FieldPrompt] = AutomationsCopy.PromptRequired;
        }
        else if (_prompt.Length > PromptMaxLength)
        {
            errors[AutomationsCopy.FieldPrompt] = AutomationsCopy.PromptTooLong;
        }
    }

    private void ValidateTriggers(Dictionary<string, string> errors)
    {
        var hasCron = _cronText.Trim().Length > 0;
        if (!hasCron && !_webhookEnabled && _extraTriggers.Count == 0)
        {
            errors[AutomationsCopy.FieldTriggers] = AutomationsCopy.TriggersRequired;
        }

        if (hasCron && _previewError.Length > 0 && string.Equals(_previewKey, PreviewKeyOf(_cronText.Trim(), EffectiveTimeZone()), StringComparison.Ordinal))
        {
            errors[AutomationsCopy.FieldCron] = _previewError;
        }

        if (!_webhookEnabled)
        {
            return;
        }

        var generic = string.Equals(_webhookFlavour, GenericFlavour, StringComparison.Ordinal);
        if (!generic && !IsGithubWebhook)
        {
            errors[AutomationsCopy.FieldWebhook] = AutomationsCopy.WebhookFlavourInvalid;
        }
        else if (IsGithubWebhook && !AreFiltersValid())
        {
            errors[AutomationsCopy.FieldWebhook] = AutomationsCopy.WebhookFilterInvalid;
        }
    }

    private bool AreFiltersValid() =>
        SplitList(_webhookEventsText).All(IsFilterName)
        && SplitList(_webhookActionsText).All(IsFilterName)
        && SplitList(_webhookBranchesText).All(IsBranchName);

    private void ValidateTarget(Dictionary<string, string> errors)
    {
        if (!_supportedTargetKinds.Contains(_targetKind, StringComparer.Ordinal))
        {
            errors[AutomationsCopy.FieldTarget] = AutomationsCopy.TargetUnsupported;
            return;
        }

        if (!IsCloudTarget)
        {
            return;
        }

        var repositories = ParseRepositories(_repositoriesText, out var valid);
        var lines = _repositoriesText.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        if (lines == 0)
        {
            errors[AutomationsCopy.FieldRepositories] = AutomationsCopy.RepositoriesRequired;
        }
        else if (lines > MaxRepositories)
        {
            errors[AutomationsCopy.FieldRepositories] = AutomationsCopy.RepositoriesTooMany;
        }
        else if (!valid || repositories.Count != lines)
        {
            errors[AutomationsCopy.FieldRepositories] = AutomationsCopy.RepositoryInvalid;
        }

        if (!SplitList(_secretRefsText).All(IsSecretName))
        {
            errors[AutomationsCopy.FieldSecrets] = AutomationsCopy.SecretNameInvalid;
        }
    }

    private void ValidateLimits(Dictionary<string, string> errors)
    {
        var problems = new List<string>();
        if (!TryParseLimit(_maxConcurrentRunsText, 1, 4, out _))
        {
            problems.Add(AutomationsCopy.ConcurrentRunsInvalid);
        }

        if (!TryParseLimit(_maxRunsPerHourText, 1, 60, out _))
        {
            problems.Add(AutomationsCopy.RunsPerHourInvalid);
        }

        if (!TryParseLimit(_maxRuntimeMinutesText, 1, 360, out _))
        {
            problems.Add(AutomationsCopy.RuntimeInvalid);
        }

        if (problems.Count > 0)
        {
            errors[AutomationsCopy.FieldLimits] = string.Join(' ', problems);
        }
    }

    private bool Edit<T>(ref T field, T value, string property, params string[] touched)
    {
        if (!SetProperty(ref field, value, property))
        {
            return false;
        }

        foreach (var name in touched)
        {
            _touched.Add(name);
        }

        return true;
    }

    private void ClearPreview()
    {
        if (_previewLines.Length == 0 && _previewError.Length == 0 && _previewKey is null)
        {
            return;
        }

        _previewLines = [];
        _previewError = string.Empty;
        _previewKey = null;
        NotifyPreview();
    }

    private void NotifyPreview()
    {
        NotifyPropertyChanged(nameof(CronPreviewLines));
        NotifyPropertyChanged(nameof(CronPreviewError));
    }

    private void Refresh()
    {
        _allErrors = Validate();
        _errors = _allErrors.Where(pair => _touched.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        NotifyPropertyChanged(nameof(Errors));
        NotifyPropertyChanged(nameof(NameError));
        NotifyPropertyChanged(nameof(PromptError));
        NotifyPropertyChanged(nameof(TriggersError));
        NotifyPropertyChanged(nameof(CronError));
        NotifyPropertyChanged(nameof(WebhookError));
        NotifyPropertyChanged(nameof(TargetError));
        NotifyPropertyChanged(nameof(RepositoriesError));
        NotifyPropertyChanged(nameof(SecretsError));
        NotifyPropertyChanged(nameof(LimitsError));
        NotifyPropertyChanged(nameof(CanSave));
        NotifyPropertyChanged(nameof(WhyDisabled));
        NotifyPropertyChanged(nameof(IsSaving));
        NotifyPropertyChanged(nameof(IsPreviewing));
        NotifyPropertyChanged(nameof(SaveFailure));
        NotifyPropertyChanged(nameof(IsEdit));
        NotifyPropertyChanged(nameof(IsGithubWebhook));
        NotifyPropertyChanged(nameof(WebhookPath));
        NotifyPropertyChanged(nameof(CanRotateSecret));
        NotifyPropertyChanged(nameof(HasWebhookSecret));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
