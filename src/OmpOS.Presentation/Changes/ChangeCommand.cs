using System.Windows.Input;

namespace OmpOS.Presentation.Changes;

/// <summary>
/// A synchronous command of the change review. <see cref="ICommand"/> is base .NET, so the models stay free of any UI framework; each head
/// binds to it. The command parameter, when it is a string, is the workspace-relative path of the file the command acts on.
/// </summary>
public sealed class ChangeCommand : ICommand
{
    private readonly Action<string?> _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>Creates a command.</summary>
    /// <param name="execute">The action. It receives the command parameter when that is a string, otherwise null.</param>
    /// <param name="canExecute">Decides whether the command may run. Null means always.</param>
    /// <exception cref="ArgumentNullException"><paramref name="execute"/> is null.</exception>
    public ChangeCommand(Action<string?> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute(parameter as string);
        }
    }

    /// <summary>Tells every binding to ask <see cref="CanExecute"/> again.</summary>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
