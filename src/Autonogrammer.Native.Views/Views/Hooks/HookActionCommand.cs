using System.Windows.Input;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// A command that starts an asynchronous hooks action. It can always run. The model reports a failure of the action as its action
/// failure text, except OutOfMemoryException, which faults the task. The command observes the task and never rethrows the fault.
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

    /// <summary>Starts the action. The task is observed so that a fault is never left unobserved; the model reports the failure.</summary>
    /// <param name="parameter">Unused.</param>
    public void Execute(object? parameter) => _ = _execute().ContinueWith(
        static task => _ = task.Exception,
        CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
}
