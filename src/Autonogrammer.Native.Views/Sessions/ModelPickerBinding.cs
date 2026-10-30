using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Presentation.Composer;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// Joins the <see cref="IOmpModelControl"/> of a running session to the <see cref="ModelPickerModel"/> of its composer: the picker lists
/// what the engine reports, a switch goes to the engine, and a change the engine makes on its own (also from another thread) reaches the
/// picker on the UI thread, so the chip follows it.
/// </summary>
internal sealed class ModelPickerBinding : IDisposable
{
    private readonly IOmpModelControl _control;
    private int _disposed;

    /// <summary>Creates the picker and shows what the control already reports.</summary>
    /// <param name="control">The model control of the session.</param>
    /// <param name="connect">Starts the engine session so that the control reports its models, or null when the control reports them without being asked.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public ModelPickerBinding(IOmpModelControl control, Func<CancellationToken, Task>? connect)
    {
        ArgumentNullException.ThrowIfNull(control);

        _control = control;
        Picker = new ModelPickerModel(SwitchAsync, connect);
        _control.ModelChanged += OnModelChanged;
        Push();
    }

    /// <summary>Gets the picker the composer shows.</summary>
    public ModelPickerModel Picker { get; }

    /// <summary>Stops following the control. The picker keeps what it shows.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _control.ModelChanged -= OnModelChanged;
        }
    }

    private async Task<ModelSwitchResult> SwitchAsync(string value, CancellationToken cancellationToken)
    {
        var result = await _control.SetModelAsync(value, cancellationToken).ConfigureAwait(true);
        return result.IsSwitched
            ? ModelSwitchResult.Switched(result.CurrentModelId ?? value)
            : ModelSwitchResult.Refused(result.Message ?? "The model could not be changed.", result.CurrentModelId);
    }

    private void OnModelChanged(object? sender, EventArgs e)
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

        var choices = _control.ModelChoices;
        var current = _control.CurrentModelId;
        if (choices.Count == 0 && current is null)
        {
            // No session behind the control (not started yet, or closed), or an engine that offers no model: the picker knows nothing.
            Picker.Reset();
            return;
        }

        Picker.Refresh([.. choices.Select(static choice => ModelChoice.Create(choice.Value, choice.Name))], current);
    }
}
