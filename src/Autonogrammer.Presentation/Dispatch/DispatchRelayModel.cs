using System.ComponentModel;

namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The model of the Dispatch settings section. It reads the account's devices, its commands and, when the head has one, this desktop's pairing
/// from the host ports, then holds the page the view draws. Before the read it holds the loading page. A failed read, or an action that failed
/// (cancel, capability, pause), becomes the page's problem line and never an exception on the view; the page then reloads so it shows what the
/// service now says. The change is raised on the context that loads the model, so the view updates on its own thread.
/// </summary>
public sealed class DispatchRelayModel : INotifyPropertyChanged
{
    private readonly IDispatchHost _host;
    private readonly IThisDeviceHost? _thisDevice;
    private DispatchPageModel _page = new(DevicesViewModel.Loading(), [], null);
    private string? _actionProblem;

    /// <summary>Initializes a new instance of the <see cref="DispatchRelayModel"/> class.</summary>
    /// <param name="host">The devices, commands and feed port.</param>
    /// <param name="thisDevice">This desktop's pairing and policy port, or null on a head that cannot pair a desktop (the page then says to pair in the desktop app).</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public DispatchRelayModel(IDispatchHost host, IThisDeviceHost? thisDevice)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _thisDevice = thisDevice;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the page the view draws. It is the loading page until <see cref="LoadAsync"/> completes.</summary>
    public DispatchPageModel Page => _page;

    /// <summary>Reads the devices, the commands and this desktop's pairing. A failed read is held as the page's problem line, or as this desktop's status.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page is ready.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var actionProblem = _actionProblem;
        _actionProblem = null;

        DevicesViewModel devices;
        try
        {
            devices = DevicesViewModel.Loaded(await _host.ListDevicesAsync(cancellationToken).ConfigureAwait(true));
        }
        catch (DispatchHostException failure)
        {
            devices = DevicesViewModel.Failed(failure);
        }

        IReadOnlyList<DispatchCommandRowModel> commands = [];
        string? readProblem = null;
        try
        {
            commands = (await _host.ListCommandsAsync(cancellationToken).ConfigureAwait(true)).Select(ToRow).ToArray();
        }
        catch (DispatchHostException failure)
        {
            readProblem = DispatchCopy.ForPhase(DispatchPhaseMap.ForHostFailure(failure.Kind));
        }

        PairThisDeviceModel? pair = null;
        if (_thisDevice is not null)
        {
            try
            {
                pair = PairThisDeviceModel.From(await _thisDevice.GetThisDeviceAsync(cancellationToken).ConfigureAwait(true));
            }
            catch (DispatchHostException failure)
            {
                pair = PairThisDeviceModel.Unavailable(failure);
            }
        }

        _page = new DispatchPageModel(devices, commands, pair, actionProblem ?? readProblem);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Page)));
    }

    /// <summary>Cancels a command that has not finished, then reloads the page.</summary>
    /// <param name="commandId">The command.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the page is reloaded.</returns>
    public Task CancelAsync(string commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        return RunAsync(() => _host.CancelAsync(commandId, cancellationToken), cancellationToken);
    }

    /// <summary>Allows or refuses one capability on this desktop, then reloads the page. Does nothing on a head with no local device.</summary>
    /// <param name="capability">The capability.</param>
    /// <param name="enabled">Whether the capability is allowed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the page is reloaded.</returns>
    public Task SetCapabilityAsync(string capability, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capability);
        var device = _thisDevice;
        return device is null
            ? Task.CompletedTask
            : RunAsync(() => device.SetCapabilityAsync(capability, enabled, cancellationToken), cancellationToken);
    }

    /// <summary>Pauses or resumes Dispatch on this desktop, then reloads the page. Does nothing on a head with no local device.</summary>
    /// <param name="paused">Whether Dispatch is paused.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the page is reloaded.</returns>
    public Task SetPausedAsync(bool paused, CancellationToken cancellationToken)
    {
        var device = _thisDevice;
        return device is null
            ? Task.CompletedTask
            : RunAsync(() => device.SetPausedAsync(paused, cancellationToken), cancellationToken);
    }

    private static DispatchCommandRowModel ToRow(DispatchCommand command) =>
        DispatchCommandRowModel.From(command.CommandId, command.Operation, command.Phase, command.TaskText, command.Reason);

    private async Task RunAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (DispatchHostException failure)
        {
            _actionProblem = DispatchCopy.ForPhase(DispatchPhaseMap.ForHostFailure(failure.Kind));
        }

        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
}
