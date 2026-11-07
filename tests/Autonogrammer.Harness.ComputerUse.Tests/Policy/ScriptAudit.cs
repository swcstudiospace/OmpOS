using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

internal sealed class ScriptAudit : IComputerUseAuditSink
{
    public bool Succeed { get; set; } = true;

    public ValueTask<bool> TryWriteAheadAsync(ComputerActionRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Succeed);
}
