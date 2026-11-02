#pragma warning disable MA0048 // The request and result travel with the composition method.
using System.Reflection;
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Tools;
using Autonogrammer.Harness.BrowserUse;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Tools;
using Autonogrammer.Harness.BrowserUse.Viewer;
using Autonogrammer.Harness.BrowserUse.Vision;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Browser;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Browser;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;

namespace Autonogrammer.Native.Cowork;

/// <summary>What <see cref="BrowserUseComposition.Wire"/> needs. Configuration is applied as <c>Cowork:BrowserUse</c> keys.</summary>
internal sealed class BrowserUseCompositionRequest
{
    /// <summary>Gets the head state directory.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the section bind. Null leaves the defaults, including Enabled false.</summary>
    public Action<BrowserUseOptions>? Configure { get; init; }

    /// <summary>Gets a scripted page source. Null never opens a page.</summary>
    public IBrowserPageSource? Pages { get; init; }

    /// <summary>Gets whether tools are offered even when locate finds no binary. Tests use this. Production is false.</summary>
    public bool ForceAvailable { get; init; }

    /// <summary>Gets the dock to register. Null skips panel registration.</summary>
    public CoworkShellModel? Shell { get; init; }

    /// <summary>Gets the header slot. Null skips the indicator.</summary>
    public SurfaceChromeModel? Chrome { get; init; }

    /// <summary>Gets the artifact tool provider added beside the browser provider. Null leaves the tool host as it was without it.</summary>
    public ICoworkToolProvider? Artifacts { get; init; }
}

/// <summary>The desktop browser composition: options, settings, gate, provider and pane.</summary>
internal sealed class BrowserUseCompositionResult : IDisposable
{
    private readonly HttpMessageHandler _handler;
    private int _disposed;
    private int _references = 1;

    internal BrowserUseCompositionResult(
        BrowserUseOptions options,
        BrowserUseSettingsSource settings,
        BrowserCallGate gate,
        TakeOverController control,
        BrowserToolProvider? provider,
        CoworkToolHost host,
        BrowserPaneViewModel pane,
        BrowserPanePanel panel,
        Func<bool> capability,
        HttpMessageHandler handler)
    {
        Options = options;
        Settings = settings;
        Gate = gate;
        Control = control;
        Provider = provider;
        Host = host;
        Pane = pane;
        Panel = panel;
        Capability = capability;
        _handler = handler;
    }

    /// <summary>Gets the bound options.</summary>
    public BrowserUseOptions Options { get; }

    /// <summary>Gets the settings source handed to <see cref="HeadSettingsOptions"/>.</summary>
    public BrowserUseSettingsSource Settings { get; }

    /// <summary>Gets the gate shared by the controller and the provider.</summary>
    public BrowserCallGate Gate { get; }

    /// <summary>Gets the registered pane control.</summary>
    public TakeOverController Control { get; }

    /// <summary>Gets the provider, or null when a disabled or uninstalled browser registered no tools.</summary>
    public BrowserToolProvider? Provider { get; }

    /// <summary>Gets the tool host. It contains the provider only when tools are registered.</summary>
    public CoworkToolHost Host { get; }

    /// <summary>Gets the pane.</summary>
    public BrowserPaneViewModel Pane { get; }

    /// <summary>Gets the dock panel.</summary>
    public BrowserPanePanel Panel { get; }



    /// <summary>Gets the capability predicate. It is <see cref="BrowserUseCapability.IsOffered"/>.</summary>
    public Func<bool> Capability { get; }

    /// <summary>Releases the original caller's reference. An attached head keeps its own reference until its sessions stop.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Release();
        }
    }

    /// <summary>Retains the composition for a head that borrows its settings source.</summary>
    /// <returns>The retained composition. The head must call <see cref="Release"/> once.</returns>
    internal BrowserUseCompositionResult Retain()
    {
        int references;
        do
        {
            references = Volatile.Read(ref _references);
            ObjectDisposedException.ThrowIf(references == 0, this);
        }
        while (Interlocked.CompareExchange(ref _references, references + 1, references) != references);

        return this;
    }

    /// <summary>Releases the head's retained reference after all browser consumers have stopped.</summary>
    internal void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0)
        {
            return;
        }

        try
        {
            _handler.Dispose();
        }
        finally
        {
            try
            {
                Gate.Dispose();
            }
            finally
            {
                Pane.Dispose();
            }
        }
    }
}

/// <summary>
/// Desktop Cowork browser composition. <see cref="BrowserUseServiceExtensions.AddBrowserUse"/> binds <c>Cowork:BrowserUse</c>.
/// The same <see cref="BrowserCallGate"/> is passed to <see cref="TakeOverController"/> and <see cref="BrowserToolProvider"/>.
/// </summary>
internal static class BrowserUseComposition
{
    /// <summary>The platform id passed to the empty embedded manifest.</summary>
    public const string PlatformId = "linux-x64";

    /// <summary>The indicator id in <see cref="SurfaceChromeModel"/>.</summary>
    public const string IndicatorId = "browser-use-indicator";

    /// <summary>Binds options, calls <see cref="BrowserUseServiceExtensions.AddBrowserUse"/>, and builds the head pieces.</summary>
    /// <param name="request">The state directory and optional section bind.</param>
    /// <returns>The composition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static BrowserUseCompositionResult Wire(BrowserUseCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = new BrowserUseOptions();
        request.Configure?.Invoke(options);
        RegisterServices(options);
        var gate = new BrowserCallGate();
        try
        {
            var handler = new SocketsHttpHandler();
            try
            {
                return Build(request, options, gate, handler);
            }
            catch
            {
                handler.Dispose();
                throw;
            }
        }
        catch
        {
            gate.Dispose();
            throw;
        }
    }

    private static BrowserUseCompositionResult Build(BrowserUseCompositionRequest request, BrowserUseOptions options, BrowserCallGate gate, HttpMessageHandler handler)
    {
        var session = DispatchProxy.Create<ICDPSession, IdleCdpSession>();
        var control = new TakeOverController(session, gate, localDesktop: true);
        var settings = new BrowserUseSettingsSource(
            options,
            new ChromiumLocator(new DiskChromiumProbe()),
            ChromiumManifest.LoadEmbedded(),
            new ChromiumAcquirer(handler),
            request.StateDirectory,
            PlatformId,
            ChromiumOsFamily.Linux);
        var located = new ChromiumLocator(new DiskChromiumProbe()).Locate(options, request.StateDirectory, ChromiumOsFamily.Linux);
        var offerTools = options.Enabled && (request.ForceAvailable || located.State == BrowserLocateState.Found);
        BrowserToolProvider? provider = null;
        if (offerTools)
        {
            provider = new BrowserToolProvider(
                options,
                new DenyingBroker(),
                new MemoryGrantStore(),
                request.Pages ?? new ClosedPages(),
                gate,
                new ClosedPorts(),
                readiness: new CoworkProviderState(CoworkProviderAvailability.Available, null));
        }

        var providers = new List<ICoworkToolProvider>();
        if (provider is not null)
        {
            providers.Add(provider);
        }

        if (request.Artifacts is not null)
        {
            providers.Add(request.Artifacts);
        }

        var host = new CoworkToolHost(
            providers,
            new CoworkPolicyGate(new MemoryGrantStore(), static () => Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip),
            new DenyingBroker(),
            new MemoryGrantStore(),
            new SwallowingAudit());
        var pane = new BrowserPaneViewModel(new FixedFeed(PaneState(options, located, request.ForceAvailable)), control);
        try
        {
            var panel = new BrowserPanePanel(pane);
            BindStopChord(panel.View, pane);
            request.Chrome?.Add(new SurfaceChromeEntry(IndicatorId, BrowserChromeCopy.AgentControlling, () => { _ = pane.StopAsync(); }, "Ctrl+Shift+."));
            var result = new BrowserUseCompositionResult(options, settings, gate, control, provider, host, pane, panel, () => BrowserUseCapability.IsOffered(options), handler);
            request.Shell?.RegisterPanel(panel);
            return result;
        }
        catch
        {
            pane.Dispose();
            throw;
        }
    }

    /// <summary>Appends guidance only when the provider is available. An over-budget fragment is refused, not trimmed.</summary>
    /// <param name="available">Whether the provider reports available.</param>
    /// <param name="baseText">The base prompt, or null to read the embedded base.</param>
    /// <returns>The composed prompt.</returns>
    public static string ComposePrompt(bool available, string? baseText = null)
    {
        var guidance = BrowserLoopGuidance.For(available);
        CoworkSystemPromptOptions.RequireWithin(guidance, CoworkSystemPromptOptions.BrowserGuidanceBytes, "browser guidance");
        return CoworkSystemPrompt.Compose(browserGuidance: available ? guidance : null, baseText: baseText);
    }

    /// <summary>Binds Ctrl+Shift+. on the pane so Stop runs while that view is in the tunnel, including when a child has focus.</summary>
    /// <param name="view">The pane view.</param>
    /// <param name="pane">The pane that Stop calls.</param>
    public static void BindStopChord(BrowserPaneView view, BrowserPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(pane);
        view.AddHandler(InputElement.KeyDownEvent, (_, args) => OnStopChord(args, pane), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static void RegisterServices(BrowserUseOptions options)
    {
        var services = new ServiceCollection();
        services.AddBrowserUse(bound => Copy(options, bound));
    }

    private static void Copy(BrowserUseOptions source, BrowserUseOptions bound)
    {
        bound.Enabled = source.Enabled;
        bound.ExecutablePath = source.ExecutablePath;
        bound.AllowDispatchSessions = source.AllowDispatchSessions;
        bound.LocalOriginsTrusted = source.LocalOriginsTrusted;
        bound.PolicyMode = source.PolicyMode;
        bound.DownloadMode = source.DownloadMode;
        bound.SandboxMode = source.SandboxMode;
        bound.PersistentProfile = source.PersistentProfile;
    }

    private static BrowserPaneState PaneState(BrowserUseOptions options, ChromiumLocation located, bool forceAvailable)
    {
        if (!options.Enabled)
        {
            return BrowserPaneState.BrowserDisabled;
        }

        if (forceAvailable)
        {
            return BrowserPaneState.Live;
        }

        return located.State switch
        {
            BrowserLocateState.Found => BrowserPaneState.Live,
            BrowserLocateState.BrowserTooOld => BrowserPaneState.BrowserTooOld,
            _ => BrowserPaneState.BrowserNotInstalled,
        };
    }

    private static void OnStopChord(KeyEventArgs args, BrowserPaneViewModel pane)
    {
        if (args.Handled || args.Key != Key.OemPeriod)
        {
            return;
        }

        var modifiers = args.KeyModifiers;
        if (!modifiers.HasFlag(KeyModifiers.Control) || !modifiers.HasFlag(KeyModifiers.Shift) || modifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        args.Handled = true;
        _ = pane.StopAsync();
    }

    private sealed class FixedFeed : IBrowserPaneFeed
    {
        public FixedFeed(BrowserPaneState state) => State = state;

        public BrowserPaneState State { get; }

        public BrowserFrame? LatestFrame => null;

        public event EventHandler? Changed
        {
            add
            {
            }

            remove
            {
            }
        }
    }

    private sealed class ClosedPages : IBrowserPageSource
    {
        public int Opens => 0;

        public ValueTask<ICDPSession> OpenAsync(string sessionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The browser is not available.");
    }

    private sealed class ClosedPorts : ILocalPortPermit
    {
        public void Open(string host, int port)
        {
        }
    }

    private sealed class DenyingBroker : ICoworkApprovalBroker
    {
        public ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CoworkApprovalDecision(CoworkApprovalOutcome.Denied));
    }

    private sealed class MemoryGrantStore : ICoworkGrantStore
    {
        public ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class SwallowingAudit : ICoworkToolAudit
    {
        public ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class DiskChromiumProbe : IChromiumProbe
    {
        public bool Exists(string path) => File.Exists(path);

        public string? ReadLink(string path)
        {
            try
            {
                return File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public int? ReadMajorVersion(string path) => null;

        public IReadOnlyList<string> ListDirectories(string path) =>
            Directory.Exists(path) ? Directory.GetDirectories(path).Select(Path.GetFileName).Where(static name => name is not null).Select(static name => name!).ToArray() : [];
    }

    /// <summary>A session that never sends CDP. Stop does not need one, and this composition does not launch Chromium.</summary>
    internal class IdleCdpSession : DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            return null;
        }
    }
}
