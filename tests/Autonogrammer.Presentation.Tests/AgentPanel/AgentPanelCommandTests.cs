using System.Windows.Input;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>The command type behind every button of the panel: gating, parameters, and faults that are routed, not lost.</summary>
public sealed class AgentPanelCommandTests
{
    /// <summary>The command passes its parameter to the action and completes after it.</summary>
    [Fact]
    public async Task ItRunsTheActionWithTheParameter()
    {
        object? seen = null;
        var command = new AgentPanelCommand(parameter =>
        {
            seen = parameter;
            return Task.CompletedTask;
        }, _ => { });

        await command.ExecuteAsync("a").ConfigureAwait(true);

        seen.ShouldBe("a");
    }

    /// <summary>A command that cannot execute does not run its action.</summary>
    [Fact]
    public async Task ItDoesNotRunWhenItCannotExecute()
    {
        var runs = 0;
        var allowed = false;
        var command = new AgentPanelCommand(
            _ =>
            {
                runs++;
                return Task.CompletedTask;
            },
            _ => { },
            _ => allowed);

        await command.ExecuteAsync(null).ConfigureAwait(true);
        ((ICommand)command).Execute(null);
        runs.ShouldBe(0);

        allowed = true;
        await command.ExecuteAsync(null).ConfigureAwait(true);

        runs.ShouldBe(1);
    }

    /// <summary>A fault of the action goes to the error handler and does not escape; cancellation is not reported.</summary>
    [Fact]
    public async Task FaultsGoToTheHandlerAndCancellationIsQuiet()
    {
        var faults = new List<Exception>();
        var failing = new AgentPanelCommand(_ => throw new InvalidOperationException("boom"), faults.Add);
        var cancelled = new AgentPanelCommand(_ => Task.FromCanceled(new CancellationToken(canceled: true)), faults.Add);

        await failing.ExecuteAsync(null).ConfigureAwait(true);
        await cancelled.ExecuteAsync(null).ConfigureAwait(true);

        faults.Single().Message.ShouldBe("boom");
    }

    /// <summary>A fault of an asynchronous action that fails after it yields is routed too.</summary>
    [Fact]
    public async Task AFaultAfterAYieldIsRouted()
    {
        var faults = new List<Exception>();
        var command = new AgentPanelCommand(
            async _ =>
            {
                await Task.Yield();
                throw new IOException("late");
            },
            faults.Add);

        await command.ExecuteAsync(null).ConfigureAwait(true);

        faults.Single().ShouldBeOfType<IOException>();
    }

    /// <summary>Notifying raises the can-execute event so bindings ask again.</summary>
    [Fact]
    public void NotifyingRaisesTheCanExecuteEvent()
    {
        var command = new AgentPanelCommand(_ => Task.CompletedTask, _ => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.NotifyCanExecuteChanged();

        raised.ShouldBe(1);
    }

    /// <summary>The action and the error handler are required.</summary>
    [Fact]
    public void ArgumentsAreRequired()
    {
        Should.Throw<ArgumentNullException>(() => new AgentPanelCommand(null!, _ => { }));
        Should.Throw<ArgumentNullException>(() => new AgentPanelCommand(_ => Task.CompletedTask, null!));
    }
}
