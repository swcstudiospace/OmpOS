using System.Collections.ObjectModel;

namespace Autonogrammer.Harness.BrowserUse.Options;

/// <summary>
/// Options for the Cowork agent browser. Bound by the head from <c>Cowork:BrowserUse</c>. This project does not bind
/// configuration. Messages from <see cref="BrowserUseOptionsValidator"/> name these keys and never a value.
/// </summary>
public sealed class BrowserUseOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:BrowserUse";

    /// <summary>Gets or sets whether the agent browser is enabled. Default false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets an explicit Chromium path. Empty means locate.</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the locate source. Default <see cref="BrowserSource.Auto"/>.</summary>
    public BrowserSource Source { get; set; } = BrowserSource.Auto;

    /// <summary>Gets or sets the minimum Chromium major version. Default 149.</summary>
    public int MinimumMajorVersion { get; set; } = 149;

    /// <summary>Gets or sets the viewport width in CSS pixels. Default 1280.</summary>
    public int ViewportWidth { get; set; } = 1280;

    /// <summary>Gets or sets the viewport height in CSS pixels. Default 800.</summary>
    public int ViewportHeight { get; set; } = 800;

    /// <summary>Gets or sets the origin policy. Default <see cref="PolicyMode.PromptOnNewOrigin"/>.</summary>
    public PolicyMode PolicyMode { get; set; } = PolicyMode.PromptOnNewOrigin;

    /// <summary>Gets or sets hosts allowed in strict mode.</summary>
    public IList<string> AllowedHosts { get; } = new Collection<string>();

    /// <summary>Gets hosts that are always denied.</summary>
    public IList<string> DeniedHosts { get; } = new Collection<string>();

    /// <summary>Gets or sets the download policy. Default <see cref="DownloadMode.Deny"/>.</summary>
    public DownloadMode DownloadMode { get; set; } = DownloadMode.Deny;

    /// <summary>Gets or sets the sandbox policy. Default <see cref="SandboxMode.Required"/>.</summary>
    public SandboxMode SandboxMode { get; set; } = SandboxMode.Required;

    /// <summary>Gets or sets whether a named profile is kept. Default false.</summary>
    public bool PersistentProfile { get; set; }

    /// <summary>Gets or sets whether dispatch-origin turns may use the browser. Default false.</summary>
    public bool AllowDispatchSessions { get; set; }

    /// <summary>Gets or sets whether granted local origins are treated as trusted. Default false.</summary>
    public bool LocalOriginsTrusted { get; set; }

    /// <summary>Gets or sets the maximum browser processes per head. Default 2.</summary>
    public int MaxConcurrentBrowsers { get; set; } = 2;

    /// <summary>Gets or sets the maximum tabs in one browser. Default 5.</summary>
    public int MaxTabs { get; set; } = 5;

    /// <summary>Gets or sets the maximum actuating actions per minute. Default 60.</summary>
    public int MaxActionsPerMinute { get; set; } = 60;

    /// <summary>Gets or sets the maximum tool calls per session. Default 500.</summary>
    public int MaxCallsPerSession { get; set; } = 500;

    /// <summary>Gets or sets the maximum model screenshots per session. Default 40.</summary>
    public int MaxScreenshotsPerSession { get; set; } = 40;

    /// <summary>Gets or sets the maximum screenshot size in bytes. Default 3 MiB, which is also the ceiling.</summary>
    public int MaxScreenshotBytes { get; set; } = 3 * 1024 * 1024;

    /// <summary>Gets or sets the screenshot long edge in pixels. Default 1280.</summary>
    public int ScreenshotLongEdge { get; set; } = 1280;

    /// <summary>Gets or sets the maximum characters returned by a page read. Default 20000.</summary>
    public int MaxReadChars { get; set; } = 20000;

    /// <summary>Gets or sets the browser working-set cap in mebibytes. Default 1536.</summary>
    public int MaxBrowserMemoryMiB { get; set; } = 1536;

    /// <summary>Gets or sets the idle timeout. Default 10 minutes.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets or sets the wall-clock lifetime. Default 2 hours.</summary>
    public TimeSpan MaxSessionLifetime { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Gets or sets the local pane frame rate. Default 10.</summary>
    public int LocalFramesPerSecond { get; set; } = 10;

    /// <summary>The host cap on <see cref="MaxScreenshotBytes"/>.</summary>
    public const int MaxScreenshotBytesCeiling = 3 * 1024 * 1024;
}
