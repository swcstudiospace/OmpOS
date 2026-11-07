
namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>One trip audit record. The coordinator writes exactly one per trip.</summary>
public interface IKillSwitchAudit
{
    /// <summary>Records a trip.</summary>
    void WriteTrip();
}
