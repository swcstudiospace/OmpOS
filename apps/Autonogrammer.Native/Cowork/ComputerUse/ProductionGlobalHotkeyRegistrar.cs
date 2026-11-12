using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.MacOS;
using Autonogrammer.Harness.ComputerUse.Windows;
using Autonogrammer.Harness.ComputerUse.X11;

namespace Autonogrammer.Native.Cowork.ComputerUse;

/// <summary>
/// Kill-chord registrar for the head. It routes to the operating-system registrar of the head's platform, which owns its own
/// thread and connection. On a platform without a registrar every registration returns <see langword="false"/>, which the
/// kill switch reports as <see cref="DenialCode.KillSwitchUnavailable"/>.
/// </summary>
internal sealed class ProductionGlobalHotkeyRegistrar : IGlobalHotkeyRegistrar, IDisposable
{
    private readonly IGlobalHotkeyRegistrar? _registrar;
    private int _disposed;

    /// <summary>Creates the registrar for <paramref name="platform"/>.</summary>
    /// <param name="platform">The head platform.</param>
    public ProductionGlobalHotkeyRegistrar(ComputerPlatform platform)
    {
        _registrar = Create(platform);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chord);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        return _registrar is null ? ValueTask.FromResult(false) : _registrar.RegisterAsync(chord, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask UnregisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _registrar is null ? ValueTask.CompletedTask : _registrar.UnregisterAsync(cancellationToken);
    }

    /// <summary>Releases the platform registrar, if one was created.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _registrar is IDisposable owned)
        {
            owned.Dispose();
        }
    }

    private static IGlobalHotkeyRegistrar? Create(ComputerPlatform platform) => platform switch
    {
        ComputerPlatform.X11 => new X11HotkeyRegistrar(new X11BackendOptions()),
        ComputerPlatform.Windows => new WindowsHotkeyRegistrar(),
        ComputerPlatform.MacOS => new MacOSHotkeyRegistrar(),
        _ => null,
    };
}
