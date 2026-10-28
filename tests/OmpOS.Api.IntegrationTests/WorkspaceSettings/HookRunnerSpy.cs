using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>An <see cref="IHookCommandRunner"/> that counts the calls it receives and starts nothing. A read-only endpoint must leave the count at zero.</summary>
internal sealed class HookRunnerSpy : IHookCommandRunner
{
    private int _calls;

    /// <summary>Gets the number of times a hook process was requested.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc />
    public Task<HookCommandOutcome> RunAsync(HookInvocation invocation, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _calls);
        return Task.FromResult(new HookCommandOutcome(0, string.Empty, string.Empty, false, false));
    }
}
