using Avalonia.Threading;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Presentation.Composer;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// Joins one option of the <see cref="IOmpSessionOptions"/> of a running session (its mode or its thinking level) to the
/// <see cref="OptionChipModel"/> of its composer. The chip exists only while the engine reports the option with values to choose from: it is
/// created and handed to the composer when the option appears and taken away again when the option is gone. A change the engine makes on its
/// own (also from another thread) reaches the chip on the UI thread, so the chip follows it.
/// </summary>
internal sealed class OptionChipBinding : IDisposable
{
    private readonly IOmpSessionOptions _control;
    private readonly string _optionId;
    private readonly string _displayName;
    private readonly Action<OptionChipModel?> _assign;
    private OptionChipModel? _chip;
    private int _disposed;

    /// <summary>Follows the option and shows what the control already reports.</summary>
    /// <param name="control">The options of the session.</param>
    /// <param name="optionId">The engine's identifier of the option, for example <c>mode</c>.</param>
    /// <param name="displayName">The name of the option as the chip says it, for example <c>Mode</c>.</param>
    /// <param name="assign">Hands the chip to the composer, or null when the option is gone.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> or <paramref name="assign"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="optionId"/> or <paramref name="displayName"/> is null, empty or white space.</exception>
    public OptionChipBinding(IOmpSessionOptions control, string optionId, string displayName, Action<OptionChipModel?> assign)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(assign);

        _control = control;
        _optionId = optionId;
        _displayName = displayName;
        _assign = assign;
        _control.OptionsChanged += OnOptionsChanged;
        Push();
    }

    /// <summary>Gets the chip the composer shows, or null while the engine reports no such option.</summary>
    public OptionChipModel? Chip => _chip;

    /// <summary>Stops following the control. The chip keeps what it shows.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _control.OptionsChanged -= OnOptionsChanged;
        }
    }

    private async Task<OptionChangeResult> ChangeAsync(string value, CancellationToken cancellationToken)
    {
        var result = await _control.SetOptionAsync(_optionId, value, cancellationToken).ConfigureAwait(true);
        return result.IsSet
            ? OptionChangeResult.Set(result.CurrentValue ?? value)
            : OptionChangeResult.Refused(result.Message ?? $"{_displayName} could not be changed.", result.CurrentValue);
    }

    private void OnOptionsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Push();
        }
        else
        {
            Dispatcher.UIThread.Post(Push);
        }
    }

    private void Push()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var option = _control.Options.FirstOrDefault(candidate => string.Equals(candidate.Id, _optionId, StringComparison.Ordinal));
        if (option is null || option.Values.Count == 0)
        {
            // No session behind the control (not started yet, or closed), or an engine that offers no such option: there is no chip.
            if (_chip is not null)
            {
                _chip = null;
                _assign(null);
            }

            return;
        }

        if (_chip is null)
        {
            _chip = new OptionChipModel(_optionId, _displayName, ChangeAsync);
            _assign(_chip);
        }

        _chip.Refresh([.. option.Values.Select(static value => OptionChoice.Create(value.Value, value.Name, value.Description))], option.CurrentValue);
    }
}
