using System.Windows.Input;

namespace Autonogrammer.Presentation.Mcp;

/// <summary>A command that starts an asynchronous MCP action. The action is a model method that reports its own failures, so the command does not observe the task.</summary>
public sealed class McpActionCommand : ICommand
{
    private readonly Func<Task> _execute;

    /// <summary>Creates a command.</summary>
    /// <param name="execute">The action.</param>
    public McpActionCommand(Func<Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
    }

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

    /// <summary>Starts the action.</summary>
    /// <param name="parameter">Unused.</param>
    public void Execute(object? parameter) => _ = _execute();

    /// <summary>Runs the action and completes when it has finished.</summary>
    /// <returns>A task that completes when the action ended.</returns>
    public Task ExecuteAsync() => _execute();
}
