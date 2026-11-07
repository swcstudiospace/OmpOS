using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

internal sealed class ScriptPrompt : IComputerApprovalPrompt, ILocalApproverAvailability
{
    private readonly ManualTimeProvider _time;

    public ScriptPrompt(ManualTimeProvider time) => _time = time;

    public int Calls { get; private set; }

    public ComputerApprovalChoice Choice { get; set; } = ComputerApprovalChoice.AllowOnce;

    public TimeSpan? AdvanceOnCall { get; set; }

    public bool HasLocalApprover { get; set; } = true;

    public ValueTask<ComputerApprovalChoice> RequestAsync(ComputerApprovalFacts facts, CancellationToken cancellationToken)
    {
        Calls++;
        if (AdvanceOnCall is TimeSpan advance)
        {
            _time.Advance(advance);
        }

        return ValueTask.FromResult(Choice);
    }
}
