using System.Windows.Input;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Isolation;

/// <summary>
/// The isolation level shown on screen. Network is always unrestricted. A probe that did not reach filesystem isolation
/// adds the degraded-state sentence. The continue-without-isolation question stays unanswered until a command runs,
/// and the session start stays not started until that answer exists.
/// </summary>
public sealed class IsolationChipModel : ObservableModel
{
    /// <summary>The notice that is always shown. Network is not fenced.</summary>
    public const string NetworkUnrestricted = "network: unrestricted";

    /// <summary>Launches the wrapper does not confine.</summary>
    public const string NotConfinedLaunches = "Terminals, hooks, remote sessions and Check connection runs are not confined.";

    /// <summary>The recorded risk that <c>~/.omp</c> stays readable because omp needs it writable.</summary>
    public const string OmpReadableRisk = "~/.omp stays readable by the agent.";

    /// <summary>The question asked before a session continues unconfined.</summary>
    public const string ContinueWithoutIsolation = "Continue without isolation?";

    /// <summary>The button that accepts an unconfined session.</summary>
    public const string ContinueWithoutIsolationButton = "Continue without isolation";

    /// <summary>The button that refuses to start.</summary>
    public const string StopButton = "Stop";

    /// <summary>Shown when Windows process containment is not applied.</summary>
    public const string IsolationProcessOnly = "Process containment is not applied.";

    private readonly string _networkNotice = NetworkUnrestricted;
    private readonly string _notConfinedText = NotConfinedLaunches;
    private readonly string _ompReadableText = OmpReadableRisk;
    private readonly string _continueQuestion = ContinueWithoutIsolation;
    private bool? _answer;

    /// <summary>Initializes a new instance of the <see cref="IsolationChipModel"/> class.</summary>
    /// <param name="level">The level name to show, for example <c>None</c> or <c>FilesystemIsolated</c>.</param>
    /// <param name="degradedCode">The degraded-state code, or null when the probe reached filesystem isolation.</param>
    /// <param name="needsConsent">True when the continue-without-isolation question is on screen.</param>
    /// <exception cref="ArgumentException"><paramref name="level"/> is blank.</exception>
    public IsolationChipModel(string level, string? degradedCode, bool needsConsent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        LevelText = level + ", " + NetworkUnrestricted;
        DegradedSentence = degradedCode is null ? null : Sentence(degradedCode);
        NeedsConsent = needsConsent;
        ContinueCommand = new ChipCommand(() => Answer = true);
        StopCommand = new ChipCommand(() => Answer = false);
    }

    /// <summary>Gets the level text. It always includes <see cref="NetworkUnrestricted"/>.</summary>
    public string LevelText { get; }

    /// <summary>Gets the always-shown network notice.</summary>
    public string NetworkNotice => _networkNotice;

    /// <summary>Gets the sentence naming launches that are not confined.</summary>
    public string NotConfinedText => _notConfinedText;

    /// <summary>Gets the sentence that <c>~/.omp</c> stays readable by the agent.</summary>
    public string OmpReadableText => _ompReadableText;

    /// <summary>Gets the degraded-state sentence, or null when the probe reached filesystem isolation.</summary>
    public string? DegradedSentence { get; }

    /// <summary>Gets the continue-without-isolation question.</summary>
    public string ContinueQuestion => _continueQuestion;

    /// <summary>Gets a value indicating whether the question and its buttons are shown.</summary>
    public bool NeedsConsent { get; }

    /// <summary>Gets the command bound to Continue without isolation.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Gets the command bound to Stop.</summary>
    public ICommand StopCommand { get; }

    /// <summary>Gets the answer, or null while the session start is waiting. True continues unconfined. False stops.</summary>
    public bool? Answer
    {
        get => _answer;
        private set => SetProperty(ref _answer, value);
    }

    /// <summary>Formats a degraded code as the sentence the screen shows.</summary>
    /// <param name="code">The degraded-state code.</param>
    /// <returns>The sentence, including the process-containment notice for <c>platform_unsupported</c>.</returns>
    public static string Sentence(string code)
    {
        var unavailable = "IsolationUnavailable(" + code + ")";
        return string.Equals(code, "platform_unsupported", StringComparison.Ordinal)
            ? IsolationProcessOnly + " " + unavailable
            : unavailable;
    }

    private sealed class ChipCommand : ICommand
    {
        private readonly Action _execute;

        public ChipCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
