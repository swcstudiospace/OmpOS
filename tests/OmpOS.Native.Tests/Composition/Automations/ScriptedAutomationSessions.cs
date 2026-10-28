using OmpOS.Native.Composition.Automations;

namespace OmpOS.Native.Tests.Composition.Automations;

/// <summary>The sessions of automation runs, scripted: every session it creates records what the target asked of it and answers as the test says.</summary>
internal sealed class ScriptedAutomationSessions : IAutomationSessions
{
    private readonly List<ScriptedAutomationSession> _created = [];

    /// <summary>Gets or sets whether the user allows the confirmation of a first run.</summary>
    public bool Allow { get; set; } = true;

    /// <summary>Gets or sets how the first turn ends.</summary>
    public AutomationSessionTurn Turn { get; set; } = AutomationSessionTurn.Done;

    /// <summary>Gets or sets a gate the turn waits on before it ends, or null to end at once.</summary>
    public TaskCompletionSource? TurnGate { get; set; }

    /// <summary>Gets the sessions created so far, in order.</summary>
    public IReadOnlyList<ScriptedAutomationSession> Created
    {
        get
        {
            lock (_created)
            {
                return [.. _created];
            }
        }
    }

    /// <inheritdoc/>
    public Task<IAutomationSession> CreateAsync(string title, string workspaceRoot, CancellationToken cancellationToken)
    {
        var session = new ScriptedAutomationSession(this, "session-" + (Created.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), title, workspaceRoot);
        lock (_created)
        {
            _created.Add(session);
        }

        return Task.FromResult<IAutomationSession>(session);
    }
}
