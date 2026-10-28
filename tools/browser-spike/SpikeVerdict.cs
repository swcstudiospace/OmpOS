using System.Text.Json;

namespace Autonogrammer.BrowserSpike;

/// <summary>The verdict printed as one line on stdout and returned as the process exit code.</summary>
internal sealed record SpikeVerdict(bool Passed, string Stage, string Detail)
{
    /// <summary>Creates a failing verdict for the stage that failed.</summary>
    public static SpikeVerdict Failed(string stage, string detail) => new(false, stage, detail);

    /// <summary>Judges the raw probe output: the element must exist, its selector must resolve to it, and its box must be non-empty.</summary>
    /// <param name="raw">The string returned by the probe script.</param>
    /// <param name="adapter">The adapter that served the page, as reported by the control.</param>
    public static SpikeVerdict FromProbe(string raw, string adapter)
    {
        ProbeReport report;
        try
        {
            report = ProbeReport.Parse(raw);
        }
        catch (JsonException ex)
        {
            return Failed("probe", $"script result was not the expected JSON: {ex.Message}; raw={raw}; adapter={adapter}");
        }

        if (!report.Found)
        {
            return Failed("probe", $"element with id go not found; adapter={adapter}");
        }

        bool selectorOk = string.Equals(report.Selector, "#go", StringComparison.Ordinal) && report.Resolves;
        bool boxOk = report.Width > 0 && report.Height > 0;
        string detail = $"adapter={adapter} selector={report.Selector} resolves={report.Resolves} " +
                        $"box=({report.X},{report.Y},{report.Width},{report.Height})";
        return selectorOk && boxOk
            ? new SpikeVerdict(true, "load+selector+box", detail)
            : new SpikeVerdict(false, "probe", detail);
    }

    /// <summary>Formats the verdict as one stdout line.</summary>
    public string ToLine() => (Passed ? "SPIKE PASS " : "SPIKE FAIL ") + Stage + ": " + Detail;
}
