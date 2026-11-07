using System.Windows.Input;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Computer;

/// <summary>
/// Indicator model. It reads <see cref="IKillSwitchControl.StateChanged"/> and Stop calls
/// <see cref="IKillSwitchControl.TripAsync"/>. Visible while armed or acting.
/// </summary>
public sealed class ComputerIndicatorViewModel : ObservableModel, IDisposable
{
    private readonly IKillSwitchControl _killSwitch;
    private bool _acting;
    private string _stateLabel = string.Empty;

    /// <summary>Creates the model and subscribes to the kill switch.</summary>
    /// <param name="killSwitch">The kill switch.</param>
    /// <param name="sessionName">Session name shown on the indicator.</param>
    /// <param name="killChord">Kill chord shown on the indicator.</param>
    /// <param name="origin">Local or Dispatch.</param>
    /// <param name="deviceName">Controller device name when the origin is Dispatch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="killSwitch"/> is null.</exception>
    public ComputerIndicatorViewModel(
        IKillSwitchControl killSwitch,
        string sessionName,
        string killChord,
        ComputerActionOrigin origin = ComputerActionOrigin.Local,
        string? deviceName = null)
    {
        ArgumentNullException.ThrowIfNull(killSwitch);
        _killSwitch = killSwitch;
        SessionName = string.IsNullOrWhiteSpace(sessionName) ? "session" : sessionName;
        KillChord = string.IsNullOrWhiteSpace(killChord) ? string.Empty : killChord;
        RemoteLabel = ComputerApprovalViewModel.FormatRemote(origin, deviceName);
        Stop = new StopCommand(this);
        _killSwitch.StateChanged += OnStateChanged;
        Apply(_killSwitch.State);
    }

    /// <summary>Gets the headline.</summary>
    public string Headline { get; } = ComputerUseCopy.IndicatorHeadline;

    /// <summary>Gets the session name.</summary>
    public string SessionName { get; }

    /// <summary>Gets the kill chord.</summary>
    public string KillChord { get; }

    /// <summary>Gets Remote (Dispatch) and the device name, or empty for a local session.</summary>
    public string RemoteLabel { get; }

    /// <summary>Gets a value indicating whether a remote label is shown.</summary>
    public bool HasRemoteLabel => RemoteLabel.Length > 0;

    /// <summary>Gets armed, idle or acting.</summary>
    public string StateLabel => _stateLabel;

    /// <summary>Gets a value indicating whether the indicator should be on screen: armed or acting.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>Gets the Stop command. It trips the kill switch.</summary>
    public ICommand Stop { get; }

    /// <summary>Marks whether a ticket is in flight. Armed and not acting reads as armed, idle.</summary>
    /// <param name="acting">Whether an action is in flight.</param>
    public void SetActing(bool acting)
    {
        _acting = acting;
        Apply(_killSwitch.State);
    }

    /// <inheritdoc />
    public void Dispose() => _killSwitch.StateChanged -= OnStateChanged;

    private void OnStateChanged(object? sender, KillSwitchStateChangedEventArgs e) => Apply(e.State);

    private void Apply(KillSwitchState state)
    {
        var armed = state == KillSwitchState.Armed;
        IsVisible = armed || _acting;
        _stateLabel = _acting ? ComputerUseCopy.Acting : armed ? ComputerUseCopy.ArmedIdle : string.Empty;
        NotifyPropertyChanged(nameof(IsVisible));
        NotifyPropertyChanged(nameof(StateLabel));
    }

    private async Task TripAsync()
    {
        await _killSwitch.TripAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private sealed class StopCommand : ICommand
    {
        private readonly ComputerIndicatorViewModel _owner;

        public StopCommand(ComputerIndicatorViewModel owner) => _owner = owner;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _ = _owner.TripAsync();
    }
}
