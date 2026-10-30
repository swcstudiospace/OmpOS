using Autonogrammer.Contracts.Controls;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The composer controls of one web session: the model picker, the mode and thinking chips and the slash palette. They are filled from
/// <c>GET /sessions/{id}/config-options</c> and <c>GET /sessions/{id}/commands</c> and kept current by <c>config_option_update</c> events.
/// An option change goes to <c>PUT /sessions/{id}/config-options/{optionId}</c>; the API accepts it only while a run holds a live idle engine,
/// otherwise it answers 409 and the chip shows the reason as its notice (the chip stays as it was, never an exception).
/// </summary>
internal sealed class WebSessionControls
{
    /// <summary>The option id of the model.</summary>
    internal const string ModelOptionId = "model";

    private const string ModeOptionId = "mode";
    private const string ThinkingOptionId = "thinking";
    private const string ThoughtLevelOptionId = "thought_level";

    private readonly IOmpApiClient _api;
    private readonly string _sessionId;
    private readonly Action<OptionChipModel?> _assignMode;
    private readonly Action<OptionChipModel?> _assignThinking;
    private readonly SlashCommandDispatcher _dispatcher = new(new SlashCommandResolver(SlashCommandCatalogue.Shipped));
    private IReadOnlyList<ConfigOption> _options = [];
    private IReadOnlyList<SlashEngineCommand> _engineCommands = [];
    private ISlashActionHost? _host;

    /// <summary>Initializes a new instance of the <see cref="WebSessionControls"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="sessionId">The session.</param>
    /// <param name="assignMode">Puts the mode chip on the composer (or removes it with null).</param>
    /// <param name="assignThinking">Puts the thinking chip on the composer (or removes it with null).</param>
    internal WebSessionControls(IOmpApiClient api, string sessionId, Action<OptionChipModel?> assignMode, Action<OptionChipModel?> assignThinking)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(assignMode);
        ArgumentNullException.ThrowIfNull(assignThinking);
        _api = api;
        _sessionId = sessionId;
        _assignMode = assignMode;
        _assignThinking = assignThinking;
        Picker = new ModelPickerModel(SwitchModelAsync);
        Palette = new SlashPaletteModel(SlashCommandCatalogue.Shipped);
    }

    /// <summary>Gets the model picker of the composer.</summary>
    internal ModelPickerModel Picker { get; }

    /// <summary>Gets the slash palette of the composer.</summary>
    internal SlashPaletteModel Palette { get; }

    /// <summary>Gets the mode chip, or null while the engine offers no mode.</summary>
    internal OptionChipModel? ModeChip { get; private set; }

    /// <summary>Gets the thinking chip, or null while the engine offers no thinking level.</summary>
    internal OptionChipModel? ThinkingChip { get; private set; }

    /// <summary>Gets the model of the session as the Kanban card shows it, or null when the engine reported none.</summary>
    internal KanbanModelInfo? CurrentModel
    {
        get
        {
            var model = Find(static option => string.Equals(option.Id, ModelOptionId, StringComparison.Ordinal));
            if (model is null)
            {
                return null;
            }

            var named = model.Values.FirstOrDefault(value => string.Equals(value.Value, model.CurrentValue, StringComparison.Ordinal));
            var choice = ModelChoice.Create(model.CurrentValue, named?.Name);
            return new KanbanModelInfo(choice.Name, string.Equals(choice.Provider, ModelChoice.OtherProvider, StringComparison.Ordinal) ? null : choice.Provider);
        }
    }

    /// <summary>Gets why the options are unavailable, or null.</summary>
    internal string? UnavailableReason { get; private set; }

    /// <summary>Sets the host that runs the native slash commands.</summary>
    /// <param name="host">The host, or null.</param>
    internal void SetActionHost(ISlashActionHost? host)
    {
        _host = host;
        Palette.SetActionHost(host);
    }

    /// <summary>Loads the options and the commands. A failure leaves the controls as they were: the composer still sends.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when both calls ended.</returns>
    internal async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Apply(await _api.GetConfigOptionsAsync(_sessionId, cancellationToken).ConfigureAwait(true));
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            UnavailableReason = WebFailureText.Describe(fault);
        }

        try
        {
            var commands = await _api.GetCommandsAsync(_sessionId, cancellationToken).ConfigureAwait(true);
            _engineCommands = commands.Available
                ? [.. commands.Commands.Select(static command => new SlashEngineCommand(command.Name, command.Description, command.InputHint))]
                : [];
            Palette.SetEngineCommands(_engineCommands);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            Palette.SetEngineCommands([]);
        }
    }

    /// <summary>Applies a <c>config-options</c> answer to the chips and the picker.</summary>
    /// <param name="response">The answer.</param>
    internal void Apply(SessionConfigOptionsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        UnavailableReason = response.Available ? null : response.Reason;
        _options = response.Available ? response.Options : [];
        Push();
    }

    /// <summary>Applies a <c>config_option_update</c> event: the option keeps its values and takes the new current value.</summary>
    /// <param name="update">The event.</param>
    internal void Apply(ConfigOptionUpdateEvent update)
    {
        ArgumentNullException.ThrowIfNull(update);
        _options = [.. _options.Select(option => string.Equals(option.Id, update.OptionId, StringComparison.Ordinal) ? option with { CurrentValue = update.CurrentValue } : option)];
        Push();
    }

    /// <summary>Resolves and runs the text of a slash command.</summary>
    /// <param name="text">The draft that starts with a slash.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>What to send, or the message to show.</returns>
    internal Task<SlashDispatch> DispatchAsync(string text, CancellationToken cancellationToken) =>
        _dispatcher.DispatchAsync(text, new SlashResolutionContext(_engineCommands, SlashDiscovery.Empty, _host), cancellationToken);

    /// <summary>Sets the mode by value or name (the target of the mode slash commands).</summary>
    /// <param name="mode">The mode value or name.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>A failure sentence, or null when the mode is set.</returns>
    internal async Task<string?> SetModeAsync(string mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mode);
        var chip = ModeChip;
        if (chip is null || chip.Choices.Count == 0)
        {
            return "This session does not offer modes.";
        }

        var choice = chip.Choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, mode, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.Name, mode, StringComparison.OrdinalIgnoreCase));
        if (choice is null)
        {
            return "The engine offers no " + mode + " mode. It offers: " + string.Join(", ", chip.Choices.Select(static offered => offered.Value)) + ".";
        }

        cancellationToken.ThrowIfCancellationRequested();
        await chip.SelectAsync(choice).ConfigureAwait(true);
        return string.Equals(chip.CurrentValue, choice.Value, StringComparison.Ordinal) ? null : chip.Notice ?? "The mode did not change.";
    }

    /// <summary>Filters the model list and picks the model when exactly one matches (the target of the model slash command).</summary>
    /// <param name="filter">The text to filter by.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sentence to show.</returns>
    internal async Task<string> PickModelAsync(string filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        await Picker.EnsureLoadedAsync(cancellationToken).ConfigureAwait(true);
        Picker.FilterText = filter;
        var matches = Picker.Rows.Where(static row => row.Choice is not null).Select(static row => row.Choice!).ToArray();
        if (matches.Length == 1)
        {
            await Picker.SelectAsync(matches[0]).ConfigureAwait(true);
            Picker.FilterText = string.Empty;
            return Picker.Notice ?? "Model: " + matches[0].Name + ".";
        }

        return matches.Length == 0
            ? "No model matches '" + filter + "'."
            : "Several models match '" + filter + "': pick one in the model list.";
    }

    private ConfigOption? Find(Func<ConfigOption, bool> match) => _options.FirstOrDefault(match);

    private void Push()
    {
        var model = Find(static option => string.Equals(option.Id, ModelOptionId, StringComparison.Ordinal));
        if (model is null || model.Values.Count == 0)
        {
            Picker.Reset();
        }
        else
        {
            Picker.Refresh([.. model.Values.Select(static value => ModelChoice.Create(value.Value, value.Name))], model.CurrentValue);
        }

        ModeChip = PushChip(
            ModeChip,
            Find(static option => string.Equals(option.Id, ModeOptionId, StringComparison.Ordinal)),
            "Mode",
            _assignMode);
        ThinkingChip = PushChip(
            ThinkingChip,
            Find(static option => string.Equals(option.Id, ThinkingOptionId, StringComparison.Ordinal)
                || string.Equals(option.Id, ThoughtLevelOptionId, StringComparison.Ordinal)
                || string.Equals(option.Category, ThoughtLevelOptionId, StringComparison.Ordinal)),
            "Thinking",
            _assignThinking);
    }

    private OptionChipModel? PushChip(OptionChipModel? chip, ConfigOption? option, string displayName, Action<OptionChipModel?> assign)
    {
        if (option is null || option.Values.Count == 0)
        {
            if (chip is not null)
            {
                assign(null);
            }

            return null;
        }

        if (chip is null || !string.Equals(chip.OptionId, option.Id, StringComparison.Ordinal))
        {
            var optionId = option.Id;
            chip = new OptionChipModel(optionId, displayName, (value, cancellationToken) => ChangeAsync(optionId, value, cancellationToken));
            assign(chip);
        }

        chip.Refresh([.. option.Values.Select(static value => OptionChoice.Create(value.Value, value.Name, value.Description))], option.CurrentValue);
        return chip;
    }

    private async Task<OptionChangeResult> ChangeAsync(string optionId, string value, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.SetConfigOptionAsync(_sessionId, optionId, value, cancellationToken).ConfigureAwait(true);
            Apply(response);
            return OptionChangeResult.Set(CurrentOf(optionId) ?? value);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return OptionChangeResult.Refused(WebFailureText.Describe(fault), CurrentOf(optionId));
        }
    }

    private async Task<ModelSwitchResult> SwitchModelAsync(string value, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.SetConfigOptionAsync(_sessionId, ModelOptionId, value, cancellationToken).ConfigureAwait(true);
            Apply(response);
            return ModelSwitchResult.Switched(CurrentOf(ModelOptionId) ?? value);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return ModelSwitchResult.Refused(WebFailureText.Describe(fault), CurrentOf(ModelOptionId));
        }
    }

    private string? CurrentOf(string optionId) =>
        _options.FirstOrDefault(option => string.Equals(option.Id, optionId, StringComparison.Ordinal))?.CurrentValue;
}
