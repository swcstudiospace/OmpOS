using System.Windows.Input;

namespace OmpOS.Native.Views.Editor;

/// <summary>A command that runs an action. It can always run, so it never raises <see cref="CanExecuteChanged"/>.</summary>
internal sealed class EditorCommand(Action execute) : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    /// <summary>Never raised: the command can always run.</summary>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <summary>Returns true: the command can always run.</summary>
    /// <param name="parameter">Unused.</param>
    /// <returns>Always true.</returns>
    public bool CanExecute(object? parameter) => true;

    /// <summary>Runs the action.</summary>
    /// <param name="parameter">Unused.</param>
    public void Execute(object? parameter) => _execute();
}
