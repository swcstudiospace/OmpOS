using OmpOS.Harness.Abstractions;
using OmpOS.Harness.ContractTests;

namespace OmpOS.Harness.InMemory.Tests;

/// <summary>Runs the shared session contract against the in-memory adapter.</summary>
public sealed class InMemoryOmpSessionContractTests : OmpSessionContractTests<IOmpSession>
{
    private readonly InMemoryOmpAdapter _adapter = new(ScriptFixtures.Emit(ScriptFixtures.HappySequence));

    /// <inheritdoc />
    protected override IOmpSession CreateSession() => _adapter.CreateSession();

    /// <inheritdoc />
    protected override IReadOnlyList<HarnessEvent> ScriptedSequence => ScriptFixtures.HappySequence;
}
