using Autonogrammer.Harness.ComputerUse.Audit;
using Autonogrammer.Harness.ComputerUse.Frames;
using Autonogrammer.Harness.ComputerUse.Tools;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>Wired frame, audit and provider objects.</summary>
public sealed class ComputerUseTools
{
    /// <summary>Frame pipeline.</summary>
    public required VisionPipeline Frames { get; init; }

    /// <summary>Audit log.</summary>
    public required ComputerAuditLog Audit { get; init; }

    /// <summary>Provider.</summary>
    public required ComputerToolProvider Provider { get; init; }

    /// <summary>Cowork tool-audit sink. It has no ledger.</summary>
    public required CoworkToolAuditSinkAdapter ToolAudit { get; init; }
}
