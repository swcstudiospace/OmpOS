using OmpOS.Harness.Automations.Hosted;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>A store, a clock, a scripted cloud target and a runner wired together for one test.</summary>
internal sealed class RunnerFixture : IAsyncDisposable
{
    /// <summary>Creates the fixture.</summary>
    public RunnerFixture()
    {
        Clock = new FakeTimeProvider(Kit.Start);
        Store = new InMemoryAutomationStore();
        Target = new ScriptedTarget();
        Runner = new AutomationRunner(Store, [Target], Clock);
    }

    /// <summary>Gets the clock.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>Gets the store.</summary>
    public InMemoryAutomationStore Store { get; }

    /// <summary>Gets the target.</summary>
    public ScriptedTarget Target { get; }

    /// <summary>Gets the runner.</summary>
    public AutomationRunner Runner { get; }

    /// <summary>Saves an automation to the store so its history can be written.</summary>
    /// <param name="automation">The automation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The automation.</returns>
    public async Task<UserAutomation> SaveAsync(UserAutomation automation, CancellationToken cancellationToken)
    {
        await Store.SaveAsync(automation, cancellationToken).ConfigureAwait(false);
        return automation;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Runner.DisposeAsync();
}
