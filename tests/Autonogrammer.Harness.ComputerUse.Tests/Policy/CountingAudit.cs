using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

internal sealed class CountingAudit : IKillSwitchAudit
{
    public int Trips { get; private set; }

    public void WriteTrip() => Trips++;
}
