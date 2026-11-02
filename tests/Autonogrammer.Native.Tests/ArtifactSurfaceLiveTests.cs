using System.Security.Cryptography;
using System.Text.Json;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Native.Views.Cowork.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// Live probe of the desktop artifact surface on a real WebKitGTK web view (D-20, D-21, D-25). It runs only when
/// <c>AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1</c>, under a private display, and it never touches the session DISPLAY:
/// <c>AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE=1 xvfb-run -a dotnet test tests/Autonogrammer.Native.Tests -c Release --no-build -- --filter-trait "Category=Live"</c>.
/// The probe loads the hostile Html fixtures from the Cowork.Artifacts tests and reads back what the page can reach: resource
/// requests, frames, parent access, the four bridge names, and the navigations the surface cancelled. Without the variable every
/// test here skips, so the offline gate never starts a desktop session.
/// </summary>
[Trait("Category", "Live")]
public sealed class ArtifactSurfaceLiveTests
{
    private const string OptInVariable = "AUTONOGRAMMER_LIVE_ARTIFACT_SURFACE";
    private const string MermaidSha256 = "a8a6a6f805b074a8261a2825c86f99c8054acb3b35f50bb7e24a4f9f2c3c3b9f";
    private const int HtmlFixtureCount = 7;
    private const string ProbeScript = """
        JSON.stringify({
          invokeCSharpAction: typeof window.invokeCSharpAction,
          chromeWebview: !!(window.chrome && window.chrome.webview),
          webkitMessageHandlers: !!(window.webkit && window.webkit.messageHandlers),
          externalPostMessage: typeof (window.external && window.external.postMessage),
          frames: window.frames.length,
          parentIsSelf: window.parent === window,
          resourceEntries: performance.getEntriesByType('resource').length
        })
        """;

    private static readonly string RequestSha = new('0', 64);
    private static readonly string[] NavigatingFixtures = ["location-assign", "meta-refresh", "window-open"];
    private static readonly TimeSpan LoadBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(2);

    /// <summary>Each hostile Html fixture loads, reaches no request, frame, parent or bridge, and every navigation it attempts is cancelled and reported.</summary>
    [Fact]
    public void HostileHtmlFixturesReachNoRequestFrameParentOrBridgeOnARealWebView()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 and run under xvfb-run -a with --filter-trait \"Category=Live\" to probe the desktop surface.");
        }

        var run = RunOnDesktop(Path.Combine(ShellHarness.RepoRoot(), "tests", "Autonogrammer.Cowork.Artifacts.Tests", "Fixtures", "hostile"));
        run.Reports.Count.ShouldBe(HtmlFixtureCount, "every hostile Html fixture must produce a report");
        foreach (var report in run.Reports)
        {
            using var probe = ParseProbe(report.Json);
            var root = probe.RootElement;
            root.GetProperty("resourceEntries").GetInt32().ShouldBe(0, report.Fixture + ": a request left the surface");
            root.GetProperty("frames").GetInt32().ShouldBe(0, report.Fixture + ": the document has frames");
            root.GetProperty("parentIsSelf").GetBoolean().ShouldBeTrue(report.Fixture + ": the document reached its parent");
            root.GetProperty("invokeCSharpAction").GetString().ShouldBe("undefined", report.Fixture + ": invokeCSharpAction is reachable");
            root.GetProperty("chromeWebview").GetBoolean().ShouldBeFalse(report.Fixture + ": window.chrome.webview is reachable");
            root.GetProperty("webkitMessageHandlers").GetBoolean().ShouldBeFalse(report.Fixture + ": window.webkit.messageHandlers is reachable");
            root.GetProperty("externalPostMessage").GetString().ShouldBe("undefined", report.Fixture + ": window.external.postMessage is reachable");
            (report.State is ArtifactSurfaceState.Running or ArtifactSurfaceState.NavigationBlocked)
                .ShouldBeTrue(report.Fixture + " ended in " + report.State);
            if (NavigatingFixtures.Contains(report.Fixture, StringComparer.Ordinal))
            {
                report.Blocked.Count.ShouldBeGreaterThan(0, report.Fixture + ": the navigation was not cancelled and reported");
            }
        }
    }

    /// <summary>The Mermaid fixture is shown through the bundle when it is embedded, and otherwise falls back to its source as Unavailable(MermaidAssetMissing).</summary>
    [Fact]
    public void TheMermaidFixtureIsShownThroughTheBundleOrItsSource()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 and run under xvfb-run -a with --filter-trait \"Category=Live\" to probe the desktop surface.");
        }

        var run = RunOnDesktop(Path.Combine(ShellHarness.RepoRoot(), "tests", "Autonogrammer.Cowork.Artifacts.Tests", "Fixtures", "hostile"));
        run.MermaidState.ShouldNotBeNull("the Mermaid fixture was not probed");
        if (new EmbeddedMermaidAssetProvider().GetScript() is null)
        {
            run.MermaidState.ShouldBe(ArtifactSurfaceState.Unavailable);
            run.MermaidReason.ShouldBe(ArtifactSurfaceReason.MermaidAssetMissing);
        }
        else
        {
            run.MermaidState.ShouldBe(ArtifactSurfaceState.Running);
        }
    }

    /// <summary>The embedded mermaid.min.js bytes hash to the pinned value (D-25). Passes only once 71-06 embeds the bundle.</summary>
    [Fact]
    public void TheEmbeddedMermaidBundleHashesToThePinnedValue()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip($"Set {OptInVariable}=1 to hash the embedded Mermaid bundle. The pinned hash passes only after 71-06 embeds the bundle.");
        }

        using var stream = typeof(EmbeddedMermaidAssetProvider).Assembly.GetManifestResourceStream(EmbeddedMermaidAssetProvider.ResourceName);
        stream.ShouldNotBeNull("mermaid.min.js is not embedded, so every Mermaid version shows Unavailable(MermaidAssetMissing) until 71-06 adds it");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray())).ShouldBe(MermaidSha256, "the embedded Mermaid bundle does not match the pinned SHA-256 (D-25)");
    }

    private static LiveRun RunOnDesktop(string hostileRoot)
    {
        var run = new LiveRun();
        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .Start((_, _) => Dispatcher.UIThread.Post(() => _ = ProbeAllAsync(run, hostileRoot)), []);
        return run;
    }

    private static async Task ProbeAllAsync(LiveRun run, string hostileRoot)
    {
        try
        {
            var html = Path.Combine(hostileRoot, "html");
            foreach (var path in Directory.EnumerateFiles(html, "*.html").Order(StringComparer.Ordinal))
            {
                run.Reports.Add(await ProbeHtmlAsync(path).ConfigureAwait(true));
            }

            var mermaid = Path.Combine(hostileRoot, "mermaid", "init-directive.mmd");
            var mermaidBytes = await File.ReadAllBytesAsync(mermaid, CancellationToken.None).ConfigureAwait(true);
            var content = new ArtifactContent(ArtifactKind.Mermaid, "init-directive", mermaidBytes);
            var assets = new EmbeddedMermaidAssetProvider();
            var surface = new NativeWebViewArtifactSurface(request => ArtifactDocumentBuilder.Build(content, request.ApprovedOrigins, assets));
            var window = new Window { Content = surface.View, Width = 480, Height = 320 };
            window.Show();
            try
            {
                await surface.LoadAsync(new ArtifactSurfaceRequest("live", 1, RequestSha, ArtifactKind.Mermaid, []), CancellationToken.None).ConfigureAwait(true);
                run.MermaidState = surface.State;
                run.MermaidReason = surface.Reason;
            }
            finally
            {
                surface.Destroy();
                window.Close();
            }
        }
        finally
        {
            Dispatcher.UIThread.InvokeShutdown();
        }
    }

    private static async Task<ProbeReport> ProbeHtmlAsync(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var htmlBytes = await File.ReadAllBytesAsync(path, CancellationToken.None).ConfigureAwait(true);
        var content = new ArtifactContent(ArtifactKind.Html, name, htmlBytes);
        var assets = new EmbeddedMermaidAssetProvider();
        var surface = new NativeWebViewArtifactSurface(request => ArtifactDocumentBuilder.Build(content, request.ApprovedOrigins, assets));
        var blocked = new List<string>();
        surface.NavigationBlocked += (_, e) => blocked.Add(e.Url.OriginalString);
        var window = new Window { Content = surface.View, Width = 480, Height = 320 };
        window.Show();
        try
        {
            await surface.LoadAsync(new ArtifactSurfaceRequest("live", 1, RequestSha, ArtifactKind.Html, []), CancellationToken.None).ConfigureAwait(true);
            await WaitUntilLoadedAsync(surface).ConfigureAwait(true);
            await Task.Delay(SettleDelay).ConfigureAwait(true);
            var webView = surface.View.Child as NativeWebView ?? throw new InvalidOperationException("The surface holds no web view.");
            var json = (await webView.InvokeScript(ProbeScript).ConfigureAwait(true)) ?? throw new InvalidOperationException("The probe returned no result.");
            return new ProbeReport(name, surface.State, json, blocked);
        }
        finally
        {
            surface.Destroy();
            window.Close();
        }
    }

    private static async Task WaitUntilLoadedAsync(NativeWebViewArtifactSurface surface)
    {
        var deadline = DateTimeOffset.UtcNow + LoadBudget;
        while (surface.State is ArtifactSurfaceState.Loading or ArtifactSurfaceState.Idle && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100).ConfigureAwait(true);
        }
    }

    private static JsonDocument ParseProbe(string json)
    {
        var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.String)
        {
            return document;
        }

        var inner = document.RootElement.GetString() ?? throw new InvalidOperationException("The probe returned no text.");
        document.Dispose();
        return JsonDocument.Parse(inner);
    }

    private sealed record ProbeReport(string Fixture, ArtifactSurfaceState State, string Json, IReadOnlyList<string> Blocked);

    private sealed class LiveRun
    {
        public List<ProbeReport> Reports { get; } = [];

        public ArtifactSurfaceState? MermaidState { get; set; }

        public ArtifactSurfaceReason MermaidReason { get; set; }
    }
}
