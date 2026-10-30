using System.Text;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.InMemory;

/// <summary>
/// The control surfaces of the in-memory session: a model, a mode and a thinking level that can be changed while the session is idle, a fixed
/// list of slash commands, and live steering that accepts a message while a prompt streams. Everything is deterministic, so tests and the
/// development API exercise the same contract an ACP session offers without an engine.
/// </summary>
public sealed partial class InMemoryOmpSession
{
    /// <summary>The identifier of the model option, mode option and thinking option the session reports.</summary>
    private const string ModeOptionId = "mode";
    private const string ThinkingOptionId = "thought_level";
    private const int MaxSteerBytes = 8192;

    private static readonly OmpModelChoice[] Models =
    [
        new("inmemory/fast", "Fast"),
        new("inmemory/deep", "Deep"),
    ];

    private static readonly OmpSessionOptionValue[] ModeValues =
    [
        new("agent", "Agent", "The agent edits files and runs tools."),
        new("ask", "Ask", "The agent answers without changing anything."),
        new("plan", "Plan", "The agent plans before it acts."),
    ];

    private static readonly OmpSessionOptionValue[] ThinkingValues =
    [
        new("low", "Low", null),
        new("medium", "Medium", null),
        new("high", "High", null),
    ];

    private readonly System.Threading.Lock _controlGate = new();
    private readonly List<string> _steered = [];
    private string _modelId = Models[0].Value;
    private string _modeId = ModeValues[0].Value;
    private string _thinkingId = ThinkingValues[1].Value;
    private IReadOnlyList<OmpCommand> _commands =
    [
        new OmpCommand("plan", "Switch to plan mode.", null),
        new OmpCommand("model", "Switch the model.", "[model-id]"),
        new OmpCommand("compact", "Summarise the conversation so far.", null),
    ];

    /// <inheritdoc />
    public IReadOnlyList<OmpModelChoice> ModelChoices => Models;

    /// <inheritdoc />
    public string? CurrentModelId
    {
        get
        {
            lock (_controlGate)
            {
                return _modelId;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options
    {
        get
        {
            lock (_controlGate)
            {
                return
                [
                    new OmpSessionOption(ModeOptionId, "Mode", "mode", _modeId, ModeValues),
                    new OmpSessionOption(ThinkingOptionId, "Thinking", "thought_level", _thinkingId, ThinkingValues),
                ];
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands
    {
        get
        {
            lock (_controlGate)
            {
                return _commands;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <summary>Gets the messages live steering took into the running prompt, in the order they arrived.</summary>
    public IReadOnlyList<string> SteeredMessages
    {
        get
        {
            lock (_controlGate)
            {
                return [.. _steered];
            }
        }
    }

    /// <inheritdoc />
    bool IOmpLiveSteering.IsSupported => IsOpen();

    /// <summary>Replaces the advertised commands, the way an engine does when its command list changes, and raises <see cref="CommandsChanged"/>.</summary>
    /// <param name="commands">The new list.</param>
    public void AdvertiseCommands(IReadOnlyList<OmpCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        lock (_controlGate)
        {
            _commands = [.. commands];
        }

        CommandsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen())
        {
            return Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.NotOpen, CurrentModelId, "The session is not open."));
        }

        if (IsBusy())
        {
            return Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.Busy, CurrentModelId, "A prompt is running."));
        }

        if (!Models.Any(model => string.Equals(model.Value, value, StringComparison.Ordinal)))
        {
            return Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.UnknownModel, CurrentModelId, "The engine does not offer that model."));
        }

        lock (_controlGate)
        {
            _modelId = value;
        }

        ModelChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, value, null));
    }

    /// <inheritdoc />
    public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configId);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen())
        {
            return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.NotOpen, null, "The session is not open."));
        }

        if (IsBusy())
        {
            return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.Busy, null, "A prompt is running."));
        }

        OmpSessionOptionValue[] values;
        if (string.Equals(configId, ModeOptionId, StringComparison.Ordinal))
        {
            values = ModeValues;
        }
        else if (string.Equals(configId, ThinkingOptionId, StringComparison.Ordinal))
        {
            values = ThinkingValues;
        }
        else
        {
            return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.UnknownOption, null, "The engine offers no option with that identifier."));
        }

        if (!values.Any(candidate => string.Equals(candidate.Value, value, StringComparison.Ordinal)))
        {
            return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.UnknownValue, CurrentValueOf(configId), "The option does not offer that value."));
        }

        lock (_controlGate)
        {
            if (string.Equals(configId, ModeOptionId, StringComparison.Ordinal))
            {
                _modeId = value;
            }
            else
            {
                _thinkingId = value;
            }
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.Set, value, null));
    }

    /// <inheritdoc />
    Task<OmpSteerResult> IOmpLiveSteering.SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen() || State is not (HarnessState.Streaming or HarnessState.Cancelling))
        {
            return Task.FromResult(new OmpSteerResult(OmpSteerOutcome.NotRunning, queue, "No turn is running."));
        }

        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > MaxSteerBytes)
        {
            return Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Failed, queue, "The message must be 1 to 8192 bytes and not blank."));
        }

        lock (_controlGate)
        {
            _steered.Add(text);
        }

        return Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Injected, queue, null));
    }

    private string CurrentValueOf(string configId)
    {
        lock (_controlGate)
        {
            return string.Equals(configId, ModeOptionId, StringComparison.Ordinal) ? _modeId : _thinkingId;
        }
    }

    private bool IsOpen() => SessionId is not null && State is not (HarnessState.Disposed or HarnessState.Disconnected or HarnessState.Faulted);

    private bool IsBusy() => State is HarnessState.Streaming or HarnessState.Cancelling;
}
