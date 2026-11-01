namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Computer Use options. The documented key path is <see cref="SectionName"/> (<c>Cowork:ComputerUse</c>).
/// Defaults leave the feature off. The head validates these before it builds any service.
/// </summary>
public sealed class ComputerUseOptions
{
    /// <summary>Documented key path. Environment variables replace each colon with a double underscore.</summary>
    public const string SectionName = "Cowork:ComputerUse";

    /// <summary>Key <c>Cowork:ComputerUse:Enabled</c>. Default <see langword="false"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Key <c>Cowork:ComputerUse:AllowDispatchSessions</c>. Default <see langword="false"/>.</summary>
    public bool AllowDispatchSessions { get; set; }

    /// <summary>Key <c>Cowork:ComputerUse:KillChord</c>. Default Ctrl+Alt+Shift+Escape.</summary>
    public string KillChord { get; set; } = "Ctrl+Alt+Shift+Escape";

    /// <summary>Key <c>Cowork:ComputerUse:RatePerMinute</c>. Default 60.</summary>
    public int RatePerMinute { get; set; } = 60;

    /// <summary>Key <c>Cowork:ComputerUse:RateBurst</c>. Default 10.</summary>
    public int RateBurst { get; set; } = 10;

    /// <summary>Key <c>Cowork:ComputerUse:StepLimit</c>. Default 300.</summary>
    public int StepLimit { get; set; } = 300;

    /// <summary>Key <c>Cowork:ComputerUse:StepContinueGrant</c>. Default 100.</summary>
    public int StepContinueGrant { get; set; } = 100;

    /// <summary>Key <c>Cowork:ComputerUse:FailurePause</c>. Default 5.</summary>
    public int FailurePause { get; set; } = 5;

    /// <summary>Key <c>Cowork:ComputerUse:WaitSecondsMax</c>. Default 5.</summary>
    public int WaitSecondsMax { get; set; } = 5;

    /// <summary>Key <c>Cowork:ComputerUse:TypeCharCap</c>. Default 2000.</summary>
    public int TypeCharCap { get; set; } = 2000;

    /// <summary>Key <c>Cowork:ComputerUse:TakeoverPx</c>. Default 24.</summary>
    public int TakeoverPx { get; set; } = 24;

    /// <summary>Key <c>Cowork:ComputerUse:SettleMs</c>. Default 400. Allowed range 100 to 2000.</summary>
    public int SettleMs { get; set; } = 400;

    /// <summary>Key <c>Cowork:ComputerUse:PostActionFrame</c>. Default <see cref="PostActionFrame.Auto"/>.</summary>
    public PostActionFrame PostActionFrame { get; set; } = PostActionFrame.Auto;

    /// <summary>Key <c>Cowork:ComputerUse:LongEdgePx</c>. Default 1280.</summary>
    public int LongEdgePx { get; set; } = 1280;

    /// <summary>Key <c>Cowork:ComputerUse:MaxImageBytes</c>. Default 3145728 (3 MiB).</summary>
    public int MaxImageBytes { get; set; } = 3_145_728;

    /// <summary>Key <c>Cowork:ComputerUse:MaxImagesPerSession</c>. Default 80.</summary>
    public int MaxImagesPerSession { get; set; } = 80;

    /// <summary>Key <c>Cowork:ComputerUse:ModelOverrides</c>. Model id to long-edge pixels. Empty by default.</summary>
    public IReadOnlyDictionary<string, int> ModelOverrides { get; private set; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Replaces <see cref="ModelOverrides"/>. The reader uses this so the property setter stays private.</summary>
    /// <param name="overrides">The table. Keys are model ids.</param>
    public void ReplaceModelOverrides(IReadOnlyDictionary<string, int> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ModelOverrides = overrides;
    }

    /// <summary>Key <c>Cowork:ComputerUse:AuditThumbnails</c>. Default <see langword="false"/>.</summary>
    public bool AuditThumbnails { get; set; }
}
