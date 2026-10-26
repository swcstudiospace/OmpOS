using System.Windows.Input;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// A command of the Agent Panel models. <see cref="ICommand"/> is base .NET, so the models stay free of any UI framework; each head
/// binds to it. An asynchronous command never leaves a fault unobserved: every fault except cancellation goes to the handler given at creation.
/// </summary>
public sealed class AgentPanelCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Action<Exception> _onError;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>Creates a command that runs an asynchronous action.</summary>
    /// <param name="execute">The action. It receives the command parameter.</param>
    /// <param name="onError">Receives each fault of <paramref name="execute"/> except cancellation. It runs on the context that started the command.</param>
    /// <param name="canExecute">Decides whether the command may run. Null means always.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> or <paramref name="onError"/> is null.</exception>
    public AgentPanelCommand(Func<object?, Task> execute, Action<Exception> onError, Func<object?, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(onError);

        _execute = execute;
        _onError = onError;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => _ = ExecuteAsync(parameter);

    /// <summary>Runs the command and completes when it has finished. A fault goes to the error handler and does not fault the task.</summary>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>A task that completes when the action ended, was cancelled or faulted.</returns>
    public async Task ExecuteAsync(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            await _execute(parameter).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the caller's decision; nothing to report.
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            _onError(fault);
        }
    }

    /// <summary>Tells every binding to ask <see cref="CanExecute"/> again.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
