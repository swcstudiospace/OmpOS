using System.Windows.Input;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// A command that starts an asynchronous hooks action. It can always run. The action is a model method that reports its own
/// failures, so the task it starts never faults; the command therefore does not observe it.
/// </summary>
internal sealed class HookActionCommand(Func<Task> execute) : ICommand
{
    private readonly Func<Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

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

    /// <summary>Starts the action on the UI thread. The action completes on the UI thread as well.</summary>
    /// <param name="parameter">Unused.</param>
    public void Execute(object? parameter) => _ = _execute();
}
