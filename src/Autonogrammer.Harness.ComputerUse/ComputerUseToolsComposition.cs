using Autonogrammer.Harness.ComputerUse.Audit;
using Autonogrammer.Harness.ComputerUse.Frames;
using Autonogrammer.Harness.ComputerUse.Tools;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>
/// Appends frames, audit and tools on a 73-02 core. This file does not edit ComputerUseComposition.
/// The Cowork profile owns the overlay. This type never writes it.
/// </summary>
public static class ComputerUseToolsComposition
{
    /// <summary>Builds the frame pipeline, the audit log and the provider.</summary>
    /// <param name="core">The 73-02 core.</param>
    /// <param name="options">Head-supplied tool dependencies.</param>
    /// <returns>The wired tools.</returns>
    public static ComputerUseTools AddTools(ComputerUseCore core, ComputerUseToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(options);
        VisionPipeline frames = new(core.Options, options.Backend, options.Time, options.ModelId);
        ComputerAuditLog audit = new(core.StateDirectory, options.Time, core.Options.AuditThumbnails);
        ComputerToolProvider provider = new(options.Backend, core, frames, audit, options.Broker, options.Access);
        return new ComputerUseTools
        {
            Frames = frames,
            Audit = audit,
            Provider = provider,
            ToolAudit = new CoworkToolAuditSinkAdapter(audit),
        };
    }
}
