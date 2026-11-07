using Autonogrammer.Harness.ComputerUse.Abstractions;
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
    private int _disposed;

    /// <summary>Creates the result and accepts ownership only of <paramref name="ownedSettings"/>.</summary>
    /// <param name="backend">The chosen backend; its existing lifetime is unchanged.</param>
    /// <param name="status">The status adapter.</param>
    /// <param name="access">The access-facts adapter.</param>
    /// <param name="settings">The owned or borrowed settings store.</param>
    /// <param name="ownedSettings">The internally created file store, or null for a borrowed store.</param>
    public ComputerUseHeadResult(
        IComputerUseBackend backend,
        ComputerStatusAdapter status,
        ComputerAccessFactsAdapter access,
        IComputerUseSettingsStore settings,
        FileComputerUseSettingsStore? ownedSettings)
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
    }

    /// <summary>Gets the backend. NullComputerUseBackend when the platform is unsupported.</summary>
    public IComputerUseBackend Backend { get; }

    /// <summary>Gets the status adapter.</summary>
    public ComputerStatusAdapter Status { get; }

    /// <summary>Gets the access-facts adapter.</summary>
    public ComputerAccessFactsAdapter Access { get; }

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
        _ownedSettings?.Dispose();
    }

    private ComputerUseSettingsPage? CreateSettingsPage()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return null;
    }
}
