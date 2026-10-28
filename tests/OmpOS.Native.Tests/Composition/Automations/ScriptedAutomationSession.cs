using OmpOS.Native.Composition.Automations;

namespace OmpOS.Native.Tests.Composition.Automations;

/// <summary>One scripted session of an automation run.</summary>
internal sealed class ScriptedAutomationSession : IAutomationSession
{
    private readonly ScriptedAutomationSessions _owner;
    private readonly List<string> _calls = [];

    internal ScriptedAutomationSession(ScriptedAutomationSessions owner, string id, string title, string workspaceRoot)
    {
        _owner = owner;
        Id = id;
        Title = title;
        WorkspaceRoot = workspaceRoot;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <summary>Gets the title the session was created with.</summary>
    public string Title { get; }

    /// <summary>Gets the workspace root the session was created with.</summary>
    public string WorkspaceRoot { get; }

    /// <summary>Gets what the target asked: <c>confirm</c>, <c>run:&lt;prompt&gt;</c> and <c>close</c>, in order.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Gets the card the target raised, or null when it asked for none.</summary>
    public AutomationConfirmation? Confirmation { get; private set; }

    /// <inheritdoc/>
    public Task<bool> ConfirmAsync(AutomationConfirmation request, CancellationToken cancellationToken)
    {
        Record("confirm");
        Confirmation = request;
        return Task.FromResult(_owner.Allow);
    }

    /// <inheritdoc/>
    public async Task<AutomationSessionTurn> RunAsync(string prompt, CancellationToken cancellationToken)
    {
        Record("run:" + prompt);
        if (_owner.TurnGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return _owner.Turn;
    }

    /// <inheritdoc/>
    public Task CloseAsync()
    {
        Record("close");
        return Task.CompletedTask;
    }

    private void Record(string call)
    {
        lock (_calls)
        {
            _calls.Add(call);
        }
    }
}
