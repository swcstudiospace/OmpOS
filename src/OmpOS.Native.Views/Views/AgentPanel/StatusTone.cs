namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The colour role a status text or glyph stroke is drawn in. Each value maps to one token through a style class, so no view holds a colour.
/// </summary>
internal enum StatusTone
{
    /// <summary><c>OmpOS.Color.TextPrimary</c>: Running and Queued text.</summary>
    Primary = 0,

    /// <summary><c>OmpOS.Color.TextSecondary</c>: idle text and the quiet glyphs.</summary>
    Secondary = 1,

    /// <summary><c>OmpOS.Color.StatusSuccess</c>: Completed and Done.</summary>
    Success = 2,

    /// <summary><c>OmpOS.Color.StatusWarning</c>: Needs approval, Lost, Degraded and stale text.</summary>
    Warning = 3,

    /// <summary><c>OmpOS.Color.StatusError</c>: Failed and errors.</summary>
    Error = 4,

    /// <summary><c>OmpOS.Color.AccentPurple</c>: the Running glyph stroke only.</summary>
    Accent = 5,
}
