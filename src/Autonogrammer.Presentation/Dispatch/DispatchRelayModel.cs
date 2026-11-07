using System.ComponentModel;

namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The model of the Dispatch settings section. It reads the account's devices and, when the head has one, this desktop's pairing from the host
/// ports, then holds the page the view draws. Before the read it holds the loading page; a failed read becomes that page's failure state, never
/// an exception on the view. The change is raised on the context that loads the model, so the view updates on its own thread.
/// </summary>
public sealed class DispatchRelayModel : INotifyPropertyChanged
{
    private readonly IDispatchHost _host;
    private readonly IThisDeviceHost? _thisDevice;
    private DispatchPageModel _page = new(DevicesViewModel.Loading(), [], null);

    /// <summary>Initializes a new instance of the <see cref="DispatchRelayModel"/> class.</summary>
    /// <param name="host">The devices and commands port.</param>
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

    /// <summary>Reads the devices and this desktop's pairing once. A failed read is held as the page's failure state.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page is ready.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        DevicesViewModel devices;
        try
        {
            devices = DevicesViewModel.Loaded(await _host.ListDevicesAsync(cancellationToken).ConfigureAwait(true));
        }
        catch (DispatchHostException failure)
        {
            devices = DevicesViewModel.Failed(failure);
        }

        PairThisDeviceModel? pair = null;
        if (_thisDevice is not null)
        {
            try
            {
                pair = PairThisDeviceModel.From(await _thisDevice.GetThisDeviceAsync(cancellationToken).ConfigureAwait(true));
            }
            catch (DispatchHostException)
            {
                pair = null;
            }
        }

        _page = new DispatchPageModel(devices, [], pair);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Page)));
    }
}
