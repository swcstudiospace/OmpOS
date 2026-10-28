using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>An <see cref="IHarnessSessionFactory"/> that counts the engine sessions asked for. A read-only endpoint must start no engine, so the count stays zero.</summary>
internal sealed class HarnessSessionSpy : IHarnessSessionFactory
{
    private readonly ScriptedHarnessFactory _inner = new(ScriptedHarnessFactory.DeltaScript(1));
    private int _sessions;

    /// <summary>Gets the number of engine sessions created.</summary>
    public int Sessions => Volatile.Read(ref _sessions);

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        _ = Interlocked.Increment(ref _sessions);
        return _inner.CreateSession(prompt);
    }
}
