#pragma warning disable MA0048 // The request and result travel with the composition method.
using System.Collections;
using System.Reflection;
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Tools;
using Autonogrammer.Harness.BrowserUse;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tools;
using Autonogrammer.Harness.BrowserUse.Viewer;
using Autonogrammer.Harness.BrowserUse.Vision;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Browser;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Browser;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;

namespace Autonogrammer.Native.Cowork;

/// <summary>What <see cref="BrowserUseComposition.Wire"/> needs. Configuration is applied as <c>Cowork:BrowserUse</c> keys.</summary>
internal sealed class BrowserUseCompositionRequest
{
    /// <summary>Gets the head state directory.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the section bind. Null leaves the defaults, including Enabled false. Production passes <see cref="BrowserUseComposition.BindConfiguration"/>.</summary>
    public Action<BrowserUseOptions>? Configure { get; init; }

    /// <summary>Gets a scripted page source. Null falls back to <see cref="ProductionPages"/>, and to no page when that is false.</summary>
    public IBrowserPageSource? Pages { get; init; }

    /// <summary>Gets whether the desktop builds the production Chromium page source when <see cref="Pages"/> is null and a binary was located. Production sets it; tests keep the scripted source.</summary>
    public bool ProductionPages { get; init; }

    /// <summary>Gets whether tools are offered even when locate finds no binary. Tests use this. Production is false.</summary>
    public bool ForceAvailable { get; init; }

    /// <summary>Gets the dock to register. Null skips panel registration.</summary>
    public CoworkShellModel? Shell { get; init; }

    /// <summary>Gets the header slot. Null skips the indicator.</summary>
    public SurfaceChromeModel? Chrome { get; init; }

    /// <summary>Gets the artifact tool provider added beside the browser provider. Null leaves the tool host as it was without it.</summary>
    public ICoworkToolProvider? Artifacts { get; init; }

    /// <summary>Gets the Computer Use tool provider added beside the browser and artifact providers. Null leaves the tool host as it was without it.</summary>
    public ICoworkToolProvider? Computer { get; init; }

    /// <summary>Gets the permission mode the call gate reads. Manual asks on every call that no grant covers. The desktop composition never offers Skip.</summary>
    public Autonogrammer.Cowork.Tools.CoworkPermissionMode Mode { get; init; } = Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual;

    /// <summary>Gets the approval broker. Null uses the desktop broker, which asks on screen and refuses when no window is shown.</summary>
    public ICoworkApprovalBroker? Broker { get; init; }
}

/// <summary>
/// The desktop browser composition: options, settings, gate, provider and pane. <see cref="DisposeAsync"/> releases it and waits for the browser host to close.
/// <see cref="Dispose"/> releases the same resources synchronously and starts the host close without awaiting it; callers should use <c>await using</c>.
/// </summary>
internal sealed class BrowserUseCompositionResult : IDisposable, IAsyncDisposable
{
    private readonly HttpMessageHandler _handler;
    private readonly IDisposable[] _owned;
    private readonly BrowserSessionHostCloser[] _closers;
    private readonly BrowserPaneStop _stop;
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
        HttpMessageHandler handler,
        IDisposable[] owned,
        BrowserSessionHostCloser[] closers,
        BrowserPaneStop stop)
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
        _owned = owned;
        _closers = closers;
        _stop = stop;
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

    /// <summary>Releases the original caller's reference synchronously. The browser host close is started and not awaited; prefer <see cref="DisposeAsync"/>.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Release();
        }
    }

    /// <summary>Releases the original caller's reference and waits until the browser host has closed. An attached head keeps its own reference until its sessions stop.</summary>
    /// <returns>A task that completes when the release has finished.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await ReleaseAsync().ConfigureAwait(false);
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

    /// <summary>Releases the head's retained reference after all browser consumers have stopped. The host close is started, not awaited.</summary>
    internal void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0)
        {
            return;
        }

        try
        {
            ReleaseOwned();
        }
        finally
        {
            foreach (var closer in _closers)
            {
                closer.Dispose();
            }
        }
    }

    /// <summary>Releases the head's retained reference after all browser consumers have stopped. Pending Stop calls are joined and the browser host is closed before this returns.</summary>
    /// <returns>A task that completes when the last reference is released and the host has closed.</returns>
    internal async ValueTask ReleaseAsync()
    {
        if (Interlocked.Decrement(ref _references) != 0)
        {
            return;
        }

        try
        {
            await _stop.JoinAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                ReleaseOwned();
            }
            finally
            {
                foreach (var closer in _closers)
                {
                    await closer.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private void ReleaseOwned()
    {
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
                try
                {
                    Pane.Dispose();
                }
                finally
                {
                    foreach (var item in _owned)
                    {
                        item.Dispose();
                    }
                }
            }
        }
    }
}

/// <summary>
/// Starts the pane's Stop from a caller that cannot await it: the header action and the chord. Each Stop task is kept and joined by
/// <see cref="BrowserUseCompositionResult.ReleaseAsync"/>, so none is discarded and release never disposes the pane under a running Stop.
/// </summary>
internal sealed class BrowserPaneStop
{
    private readonly BrowserPaneViewModel _pane;
    private readonly Lock _gate = new();
    private readonly List<Task> _pending = [];

    /// <summary>Initializes the stop.</summary>
    /// <param name="pane">The pane that Stop calls.</param>
    public BrowserPaneStop(BrowserPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(pane);
        _pane = pane;
    }

    /// <summary>Starts Stop and returns at once. The task is kept until <see cref="JoinAsync"/> awaits it.</summary>
    public void Start()
    {
        lock (_gate)
        {
            _pending.Add(_pane.StopAsync());
        }
    }

    /// <summary>Waits for every Stop started so far.</summary>
    /// <returns>A task that completes when those Stops have finished.</returns>
    public Task JoinAsync()
    {
        Task[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _pending];
        }

        return Task.WhenAll(snapshot);
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

    /// <summary>The single local owner the desktop writes its grants and tool audit under. It matches the local automations owner.</summary>
    public const string LocalOwnerId = "local";

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
        var (grants, audit) = OpenToolStores(request.StateDirectory);
        var owned = new List<IDisposable> { grants, audit };
        var closers = new List<BrowserSessionHostCloser>();
        var broker = request.Broker ?? new CoworkApprovalBroker(windowAvailable: request.Shell is not null);
        BrowserToolProvider? provider = null;
        if (offerTools)
        {
            provider = new BrowserToolProvider(
                options,
                broker,
                grants,
                PagesFor(request, options, located, () => provider?.OriginGrants ?? throw new InvalidOperationException("The browser tools are not registered."), closers),
                gate,
                new ClosedPorts(),
                readiness: new CoworkProviderState(CoworkProviderAvailability.Available, null));
        }

        var providers = ProviderList(provider, request);

        var host = new CoworkToolHost(
            providers,
            new CoworkPolicyGate(grants, () => request.Mode),
            broker,
            grants,
            audit);
        var pane = new BrowserPaneViewModel(new FixedFeed(PaneState(options, located, request.ForceAvailable)), control);
        try
        {
            var panel = new BrowserPanePanel(pane);
            var stop = new BrowserPaneStop(pane);
            BindStopChord(panel.View, stop);
            request.Chrome?.Add(new SurfaceChromeEntry(IndicatorId, BrowserChromeCopy.AgentControlling, () => stop.Start(), "Ctrl+Shift+."));
            var result = new BrowserUseCompositionResult(options, settings, gate, control, provider, host, pane, panel, () => BrowserUseCapability.IsOffered(options), handler, owned.ToArray(), closers.ToArray(), stop);
            request.Shell?.RegisterPanel(panel);
            return result;
        }
        catch
        {
            pane.Dispose();
            throw;
        }
    }

    /// <summary>Returns the page source the provider opens pages through. A scripted source wins. The production source is built only when asked and a binary was located; otherwise no page opens.</summary>
    /// <param name="request">The composition request.</param>
    /// <param name="options">The bound options the production host runs with.</param>
    /// <param name="located">The locate result. Its binary is used only when found.</param>
    /// <param name="grants">Returns the origin grants the approval path writes to, so a granted origin reaches the proxy.</param>
    /// <param name="closers">The host closers. The production host's closer is added so release closes its browsers.</param>
    /// <returns>The page source.</returns>
    private static IBrowserPageSource PagesFor(BrowserUseCompositionRequest request, BrowserUseOptions options, ChromiumLocation located, Func<OriginGrants> grants, List<BrowserSessionHostCloser> closers)
    {
        if (request.Pages is not null)
        {
            return request.Pages;
        }

        if (!request.ProductionPages || located.State != BrowserLocateState.Found || located.ExecutablePath is not { Length: > 0 } path)
        {
            return new ClosedPages();
        }

        var (pages, host) = ProductionBrowserPages.Create(options, path, _ => grants(), new ChromiumProcessLauncher());
        closers.Add(new BrowserSessionHostCloser(host));
        return pages;
    }

    /// <summary>Builds the tool providers the host receives: the browser tool when offered, then the artifact and computer providers when passed.</summary>
    /// <param name="provider">The browser provider, or null when no tools are offered.</param>
    /// <param name="request">The composition request that carries the optional providers.</param>
    /// <returns>The providers in host order.</returns>
    private static List<ICoworkToolProvider> ProviderList(BrowserToolProvider? provider, BrowserUseCompositionRequest request)
    {
        var providers = new List<ICoworkToolProvider>();
        if (provider is not null)
        {
            providers.Add(provider);
        }

        if (request.Artifacts is not null)
        {
            providers.Add(request.Artifacts);
        }

        if (request.Computer is not null)
        {
            providers.Add(request.Computer);
        }

        return providers;
    }

    /// <summary>Opens the file-backed grant store and tool audit under the local owner. The caller owns both.</summary>
    /// <param name="stateRoot">The head state directory.</param>
    /// <returns>The grant store and the audit.</returns>
    private static (FileCoworkGrantStore Grants, FileCoworkToolAudit Audit) OpenToolStores(string stateRoot)
    {
        var grants = new FileCoworkGrantStore(stateRoot, LocalOwnerId);
        try
        {
            return (grants, new FileCoworkToolAudit(stateRoot, LocalOwnerId));
        }
        catch
        {
            grants.Dispose();
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
    /// <param name="stop">The stop the chord starts. Its task is kept until release joins it.</param>
    public static void BindStopChord(BrowserPaneView view, BrowserPaneStop stop)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(stop);
        view.AddHandler(InputElement.KeyDownEvent, (_, args) => OnStopChord(args, stop), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>
    /// Binds <c>Cowork:BrowserUse</c> from the desktop configuration. The desktop has no configuration host, so keys come from the process
    /// environment: the variable <c>Cowork__BrowserUse__Enabled</c> is the key <c>Cowork:BrowserUse:Enabled</c>. An absent key keeps the default,
    /// so Enabled stays false.
    /// </summary>
    /// <param name="options">The options to bind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static void BindConfiguration(BrowserUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DesktopConfiguration().GetSection(BrowserUseOptions.SectionName).Bind(options);
    }

    private static IConfiguration DesktopConfiguration()
    {
        const string prefix = "Cowork__";
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                values[name.Replace("__", ":", StringComparison.Ordinal)] = entry.Value as string;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
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

    private static void OnStopChord(KeyEventArgs args, BrowserPaneStop stop)
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
        stop.Start();
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
