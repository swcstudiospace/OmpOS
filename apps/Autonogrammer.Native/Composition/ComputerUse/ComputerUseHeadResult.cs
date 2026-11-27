using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Tools;
using Autonogrammer.Presentation.Computer;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>
/// What composition built. Owns only the file store explicitly transferred by composition, not a supplied store.
/// All settings operations must be awaited before disposing the result.
/// </summary>
internal sealed class ComputerUseHeadResult : IDisposable
{
    private readonly IComputerUseSettingsStore _settings;
    private readonly FileComputerUseSettingsStore? _ownedSettings;
    private readonly IDisposable[] _owned;
    private int _disposed;

    /// <summary>Creates the result and accepts ownership only of <paramref name="ownedSettings"/> and <paramref name="owned"/>.</summary>
    /// <param name="backend">The chosen backend; its existing lifetime is unchanged.</param>
    /// <param name="status">The status adapter.</param>
    /// <param name="access">The access-facts adapter.</param>
    /// <param name="settings">The owned or borrowed settings store.</param>
    /// <param name="ownedSettings">The internally created file store, or null for a borrowed store.</param>
    /// <param name="core">The gated core from <see cref="ComputerUseComposition"/>, or null when no core was built.</param>
    /// <param name="tools">The wired tools, or null when Computer Use offers no tools.</param>
    /// <param name="indicator">The indicator model over the core kill switch, or null when no core was built.</param>
    /// <param name="owned">Internally created disposables (kill coordinator, platform hotkey registrar, indicator) transferred to this result.</param>
    public ComputerUseHeadResult(
        IComputerUseBackend backend,
        ComputerStatusAdapter status,
        ComputerAccessFactsAdapter access,
        IComputerUseSettingsStore settings,
        FileComputerUseSettingsStore? ownedSettings,
        ComputerUseCore? core = null,
        ComputerUseTools? tools = null,
        ComputerIndicatorViewModel? indicator = null,
        IDisposable[]? owned = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(settings);
        Backend = backend;
        Status = status;
        Access = access;
        _settings = settings;
        SettingsFactory = CreateSettingsPage;
        _ownedSettings = ownedSettings;
        Core = core;
        Tools = tools;
        Indicator = indicator;
        _owned = owned ?? [];
    }

    /// <summary>Gets the backend. NullComputerUseBackend when the platform is unsupported.</summary>
    public IComputerUseBackend Backend { get; }

    /// <summary>Gets the status adapter.</summary>
    public ComputerStatusAdapter Status { get; }

    /// <summary>Gets the access-facts adapter.</summary>
    public ComputerAccessFactsAdapter Access { get; }

    /// <summary>Gets the gated core, or null when the platform is unsupported or only the synchronous composition ran.</summary>
    public ComputerUseCore? Core { get; }

    /// <summary>Gets the wired tools, or null when Computer Use is disabled, the platform is unsupported, or the kill switch did not arm.</summary>
    public ComputerUseTools? Tools { get; }

    /// <summary>
    /// Gets the tool provider to register in the Cowork tool host, or null when no <c>computer_*</c> tools are offered.
    /// A non-null provider still refuses every session the account gate does not admit (see <see cref="ComputerToolProvider.GetStateAsync"/>).
    /// </summary>
    public ComputerToolProvider? Provider => Tools?.Provider;

    /// <summary>Gets the kill-switch control the indicator and chrome Stop trip, or null when no core was built.</summary>
    public IKillSwitchControl? KillSwitch => Core?.KillSwitch;

    /// <summary>Gets whether the kill switch is armed. A tripped or never-armed switch denies at the policy gate.</summary>
    public bool KillArmed => Core?.KillSwitch.State == KillSwitchState.Armed;

    /// <summary>Gets the indicator model over the core kill switch, or null when no core was built.</summary>
    public ComputerIndicatorViewModel? Indicator { get; }

    /// <summary>Gets the settings store while this result is alive. Supplied stores remain caller-owned.</summary>
    public IComputerUseSettingsStore Settings
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return _settings;
        }
    }

    /// <summary>Gets the factory whose delegate identity belongs to this result.</summary>
    internal Func<ComputerUseSettingsPage?> SettingsFactory { get; }

    /// <summary>Gets or sets the predecessor captured before this result's factory is published.</summary>
    internal Func<ComputerUseSettingsPage?>? PreviousSettingsFactory { get; set; }

    /// <summary>Gets whether this result has relinquished its factory and store ownership.</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Revokes only this result's factory and disposes its explicitly owned store once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        ComputerUseHeadComposition.ReleaseSettingsFactory(this);
        try
        {
            for (int i = _owned.Length - 1; i >= 0; i--)
            {
                _owned[i].Dispose();
            }
        }
        finally
        {
            _ownedSettings?.Dispose();
        }
    }

    private ComputerUseSettingsPage? CreateSettingsPage()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return null;
    }
}
