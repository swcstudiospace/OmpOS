#pragma warning disable MA0048 // The plan owns this file for the channel, the result, the sentences and the model.
using System.Globalization;
using System.Text;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Instructions;

/// <summary>
/// The presentation face of the 75-01 <c>IProjectApi</c> global-instructions channel.
/// Method names match <c>GetGlobalInstructionsAsync</c> and <c>PutGlobalInstructionsAsync</c>.
/// This type does not reference <c>Autonogrammer.Cowork</c>, so the web head can bind the same model.
/// </summary>
public interface IGlobalInstructionsChannel
{
    /// <summary>Loads <c>/cowork/instructions/global</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instructions text.</returns>
    Task<CoworkChannelResult<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken);

    /// <summary>Saves <c>/cowork/instructions/global</c>. The model refuses an over-budget body before this is called.</summary>
    /// <param name="instructions">The instructions.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    Task<CoworkChannelResult<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken);
}

/// <summary>One account API call, without instruction text in <see cref="Reason"/>.</summary>
/// <typeparam name="T">The payload.</typeparam>
/// <param name="Succeeded">True on a 2xx response.</param>
/// <param name="Offline">True when the call did not reach the server.</param>
/// <param name="Value">The payload, or default.</param>
/// <param name="State">A D-29 state name, or null.</param>
/// <param name="Reason">A short reason that must not repeat instruction or memory text.</param>
public sealed record CoworkChannelResult<T>(bool Succeeded, bool Offline, T? Value, string? State, string? Reason);

/// <summary>Every D-29 state and the sentence a head shows for it. No new colour role is named here.</summary>
public static class CoworkDegradedSentences
{
    /// <summary>Signed out. Code is unaffected.</summary>
    public const string SignedOut = "Sign in to use Cowork. Code is unaffected.";

    /// <summary>The token expired. The cached copy is read-only.</summary>
    public const string TokenExpired = "Your sign-in expired. The cached copy is read-only. Sessions continue.";

    /// <summary>Offline. Projects are read-only.</summary>
    public const string Offline = "You are offline. Projects are read-only and memory is unavailable.";

    /// <summary>The API answered 404 cowork_disabled.</summary>
    public const string ApiDisabled = "Cowork is turned off for this account (cowork_disabled).";

    /// <summary>The API answered 429.</summary>
    public const string RateLimited = "Too many requests. Try again after the Retry-After time.";

    /// <summary>Memory is paused.</summary>
    public const string MemoryPaused = "Memory is paused. Entries stay; reads and writes do not.";

    /// <summary>Memory is off for this task.</summary>
    public const string MemoryOffForTask = "Memory is off for this task.";

    /// <summary>A never-saved class was refused.</summary>
    public const string MemoryRefusedNeverSave = "That text is in a class memory never saves.";

    /// <summary>A sensitive category was refused.</summary>
    public const string MemoryRefusedSensitive = "That text is a sensitive category. Turn on include sensitive to save it.";

    /// <summary>The memory cap was reached.</summary>
    public const string MemoryCapReached = "Memory is full. Delete an entry to save another.";

    /// <summary>A folder is missing on this device.</summary>
    public const string FolderMissing = "A project folder is missing on this device.";

    /// <summary>The project is archived.</summary>
    public const string ProjectArchived = "This project is archived.";

    /// <summary>Instructions were cut.</summary>
    public const string InstructionsTruncated = "Instructions were cut to fit the size limit.";

    /// <summary>The standing-context block was over budget.</summary>
    public const string StandingContextOverBudget = "Standing context is over budget. The memory brief was cut first.";

    /// <summary>The project file was moved aside.</summary>
    public const string ProjectFileCorrupt = "The project file was corrupt and was moved aside. The set is empty.";

    /// <summary>A scheduled run is late.</summary>
    public const string ScheduleLate = "This run is late.";

    /// <summary>A scheduled run was too old to catch up.</summary>
    public const string ScheduleSkippedTooOld = "This run was skipped because it is older than the catch-up window.";

    /// <summary>An unattended run is waiting.</summary>
    public const string WaitingForApproval = "Waiting for approval.";

    /// <summary>Approval timed out.</summary>
    public const string ApprovalTimedOut = "Approval timed out. The run was not approved.";

    /// <summary>Skip was refused.</summary>
    public const string SkipRefusedNoIsolation = "Skip was refused because the session is not isolated.";

    /// <summary>The tool host is down.</summary>
    public const string ToolHostUnavailable = "The tool host is unavailable.";

    private static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SignedOut"] = SignedOut,
        ["TokenExpired"] = TokenExpired,
        ["Offline"] = Offline,
        ["ApiDisabled"] = ApiDisabled,
        ["RateLimited"] = RateLimited,
        ["MemoryPaused"] = MemoryPaused,
        ["MemoryOffForTask"] = MemoryOffForTask,
        ["MemoryRefusedNeverSave"] = MemoryRefusedNeverSave,
        ["MemoryRefusedSensitive"] = MemoryRefusedSensitive,
        ["MemoryCapReached"] = MemoryCapReached,
        ["FolderMissing"] = FolderMissing,
        ["ProjectArchived"] = ProjectArchived,
        ["InstructionsTruncated"] = InstructionsTruncated,
        ["StandingContextOverBudget"] = StandingContextOverBudget,
        ["ProjectFileCorrupt"] = ProjectFileCorrupt,
        ["ScheduleLate"] = ScheduleLate,
        ["ScheduleSkippedTooOld"] = ScheduleSkippedTooOld,
        ["WaitingForApproval"] = WaitingForApproval,
        ["ApprovalTimedOut"] = ApprovalTimedOut,
        ["SkipRefusedNoIsolation"] = SkipRefusedNoIsolation,
        ["ToolHostUnavailable"] = ToolHostUnavailable,
    };

    /// <summary>Gets every D-29 state name this layer renders.</summary>
    public static IReadOnlyCollection<string> States => Map.Keys.ToArray();

    /// <summary>Gets the sentence for a known D-29 state, or null.</summary>
    /// <param name="state">The state name, or null.</param>
    /// <returns>The sentence, or null.</returns>
    public static string? TryFor(string? state) =>
        state is not null && Map.TryGetValue(state, out var found) ? found : null;

    /// <summary>Gets the sentence for a D-29 state name.</summary>
    /// <param name="state">The state name.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state is not a D-29 name.</exception>
    public static string For(string state) =>
        Map.TryGetValue(state, out var sentence)
            ? sentence
            : throw new ArgumentOutOfRangeException(nameof(state), state, "The degraded state is not known.");
}

/// <summary>Loading, empty, offline, error and ready. Both heads bind this.</summary>
public enum CoworkViewState
{
    /// <summary>A load is in flight.</summary>
    Loading,

    /// <summary>The load succeeded and there is nothing to show.</summary>
    Empty,

    /// <summary>The server could not be reached. Edits are refused.</summary>
    Offline,

    /// <summary>The load or save failed.</summary>
    Error,

    /// <summary>The load succeeded and there is something to show.</summary>
    Ready,
}

/// <summary>
/// Load, edit and save for account-global instructions. The 8 KiB budget matches InstructionLimits.GlobalInstructionsBytes.
/// Logs keep lengths and outcome codes only. Instruction text never enters <see cref="ToString"/>.
/// </summary>
public sealed class GlobalInstructionsModel : ObservableModel
{
    /// <summary>The InstructionLimits budget, 8 KiB.</summary>
    public const int GlobalInstructionsBytes = 8 * 1024;

    /// <summary>The limit message shown when a save is over the InstructionLimits budget.</summary>
    public const string LimitMessage = "Global instructions are limited to 8 KiB.";

    private readonly IGlobalInstructionsChannel _channel;
    private readonly List<string> _log = [];
    private string _draft = string.Empty;
    private CoworkViewState _state = CoworkViewState.Loading;
    private string? _sentence;
    private string _lastOutcome = "none";
    private bool _readOnly;

    /// <summary>Creates the model.</summary>
    /// <param name="channel">The 75-01 global-instructions channel.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is null.</exception>
    public GlobalInstructionsModel(IGlobalInstructionsChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
    }

    /// <summary>Gets the view state.</summary>
    public CoworkViewState State => _state;

    /// <summary>Gets the sentence for the current degraded or limit state, or null when ready with no notice.</summary>
    public string? Sentence => _sentence;

    /// <summary>Gets the editor text. Setting it is an edit and does not save.</summary>
    public string Draft
    {
        get => _draft;
        set
        {
            _draft = value ?? string.Empty;
            NotifyPropertyChanged(nameof(Draft));
        }
    }

    /// <summary>Gets whether save and edit are refused.</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>Gets length-and-outcome log lines. Instruction text is never written here.</summary>
    public IReadOnlyList<string> OutcomeLog => _log;

    /// <summary>Loads the account instructions.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the state is set.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Set(CoworkViewState.Loading, "Loading global instructions.", readOnly: true);
        var result = await _channel.GetGlobalInstructionsAsync(cancellationToken).ConfigureAwait(false);
        ApplyLoad(result);
    }

    /// <summary>Edits the draft. A read-only state keeps the previous draft.</summary>
    /// <param name="text">The new draft.</param>
    /// <returns>True when the draft changed.</returns>
    public bool Edit(string text)
    {
        if (_readOnly)
        {
            Note("edit-refused", _draft.Length);
            return false;
        }

        Draft = text ?? string.Empty;
        Note("edited", Encoding.UTF8.GetByteCount(Draft));
        return true;
    }

    /// <summary>Saves the draft. An over-budget draft is refused and the channel is not called.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the server accepted the save.</returns>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetByteCount(Draft);
        if (bytes > GlobalInstructionsBytes)
        {
            Set(CoworkViewState.Error, LimitMessage, readOnly: false);
            Note("refused-over-budget", bytes);
            return false;
        }

        if (_readOnly)
        {
            Note("save-refused", bytes);
            return false;
        }

        var result = await _channel.PutGlobalInstructionsAsync(Draft, cancellationToken).ConfigureAwait(false);
        if (result.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline, readOnly: true);
            Note("offline", bytes);
            return false;
        }

        if (!result.Succeeded)
        {
            Set(CoworkViewState.Error, SentenceFor(result.State) ?? "Global instructions could not be saved.", readOnly: false);
            Note(result.State ?? "error", bytes);
            return false;
        }

        Set(string.IsNullOrEmpty(Draft) ? CoworkViewState.Empty : CoworkViewState.Ready, null, readOnly: false);
        Note("saved", bytes);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"GlobalInstructionsModel bytes={Encoding.UTF8.GetByteCount(_draft)} outcome={_lastOutcome}");

    private void ApplyLoad(CoworkChannelResult<string> result)
    {
        if (result.Offline)
        {
            Set(CoworkViewState.Offline, CoworkDegradedSentences.Offline, readOnly: true);
            Note("offline", 0);
            return;
        }

        if (!result.Succeeded)
        {
            Set(CoworkViewState.Error, SentenceFor(result.State) ?? "Global instructions could not be loaded.", readOnly: true);
            Note(result.State ?? "error", 0);
            return;
        }

        Draft = result.Value ?? string.Empty;
        var empty = Draft.Length == 0;
        Set(empty ? CoworkViewState.Empty : CoworkViewState.Ready, null, readOnly: false);
        Note("loaded", Encoding.UTF8.GetByteCount(Draft));
    }

    private static string? SentenceFor(string? state) =>
        CoworkDegradedSentences.TryFor(state);

    private void Set(CoworkViewState state, string? sentence, bool readOnly)
    {
        _state = state;
        _sentence = sentence;
        _readOnly = readOnly;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
        NotifyPropertyChanged(nameof(IsReadOnly));
    }

    private void Note(string outcome, int length)
    {
        _lastOutcome = outcome;
        _log.Add(string.Create(CultureInfo.InvariantCulture, $"outcome={outcome} length={length}"));
    }
}
