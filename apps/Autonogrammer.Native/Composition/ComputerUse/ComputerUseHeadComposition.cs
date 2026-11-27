using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.MacOS;
using Autonogrammer.Harness.ComputerUse.Policy;
using Autonogrammer.Harness.ComputerUse.Windows;
using Autonogrammer.Harness.ComputerUse.X11;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Presentation.Computer;
using Autonogrammer.Presentation.Cowork;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>
/// Head-owned Computer Use glue. The OS backend is chosen for the head OS. Elsewhere the null backend reports
/// <c>computer.unavailable.platform</c>. Web never references this type.
/// </summary>
/// <remarks>
/// <see cref="CreateAsync"/> is the production path: it builds the gated core through
/// <see cref="ComputerUseComposition.CreateAsync"/>, arms the kill switch only when the validated options enable
/// Computer Use, and exposes a tool provider only when the platform is supported and the kill switch armed.
/// The chrome entry and the <see cref="ComputerIndicatorViewModel"/> Stop both trip that same kill switch, and the
/// platform hotkey registrar's pressed event trips it too. <see cref="Build"/> stays for the synchronous
/// backend/settings seams only and offers no tools. The indicator entry is added to
/// <see cref="SurfaceChromeModel"/>, which HeaderEndSlot already draws.
/// </remarks>
internal static class ComputerUseHeadComposition
{
    private static Func<ComputerUseSettingsPage?>? _settingsFactory;

    /// <summary>Gets the settings factory the window evaluates each time it opens, or null.</summary>
    public static Func<ComputerUseSettingsPage?>? SettingsFactory => Volatile.Read(ref _settingsFactory);

    /// <summary>Detects the desktop platform of this process. Linux is X11 only when an X display is present; a Wayland-only or headless session is unsupported and registers no tools.</summary>
    /// <param name="getVariable">Environment reader. Null reads the process environment.</param>
    /// <returns>The platform.</returns>
    public static ComputerPlatform DetectPlatform(Func<string, string?>? getVariable = null)
    {
        if (OperatingSystem.IsWindows())
        {
            return ComputerPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS())
        {
            return ComputerPlatform.MacOS;
        }

        if (!OperatingSystem.IsLinux())
        {
            return ComputerPlatform.Unsupported;
        }

        return DetectLinuxPlatform(getVariable ?? Environment.GetEnvironmentVariable);
    }

    /// <summary>Linux rule of <see cref="DetectPlatform"/>: X11 when <c>DISPLAY</c> is set (XWayland included), otherwise unsupported.</summary>
    internal static ComputerPlatform DetectLinuxPlatform(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        return string.IsNullOrWhiteSpace(getVariable("DISPLAY")) ? ComputerPlatform.Unsupported : ComputerPlatform.X11;
    }

    /// <summary>Chooses the backend. Unsupported platforms get <see cref="NullComputerUseBackend"/>.</summary>
    /// <param name="platform">The head platform.</param>
    /// <returns>The backend.</returns>
    public static IComputerUseBackend ChooseBackend(ComputerPlatform platform) => platform switch
    {
        ComputerPlatform.X11 => new X11Backend(),
        ComputerPlatform.Windows => new WindowsBackend(),
        ComputerPlatform.MacOS => new MacOSBackend(),
        _ => new NullComputerUseBackend(),
    };

    /// <summary>Builds the head glue.</summary>
    /// <param name="options">Platform, state directory and account gate.</param>
    /// <returns>The backend, adapters and disposable settings owner. Supplied settings remain borrowed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static ComputerUseHeadResult Build(ComputerUseHeadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var backend = ChooseBackend(options.Platform);
        var status = new ComputerStatusAdapter(backend);
        var access = new ComputerAccessFactsAdapter(options.AccountGate);
        FileComputerUseSettingsStore? ownedSettings = null;
        try
        {
            // Contract: Chrome registers before the default store is built, so a rejecting subscriber leaves no owned store.
            // With an invalid StateDirectory the store throws after the indicator entry was added; SurfaceChromeModel.Add
            // appends before it raises Changed, so that entry remains. PublishSettingsFactory never runs on failure.
            options.Chrome?.Add(new SurfaceChromeEntry(
                "computer-use-indicator",
                "Computer",
                static () => { },
                "kill"));
            IComputerUseSettingsStore settings = options.Settings
                ?? (ownedSettings = new FileComputerUseSettingsStore(options.StateDirectory));
            var result = new ComputerUseHeadResult(backend, status, access, settings, ownedSettings);
            PublishSettingsFactory(result);
            ownedSettings = null;
            return result;
        }
        finally
        {
            ownedSettings?.Dispose();
        }
    }


    /// <summary>
    /// Builds the production head glue: backend, gated core, armed kill switch and, only when the validated options
    /// enable Computer Use on a supported platform and the kill switch confirmed its grab, the tool provider the
    /// Cowork tool host registers. An unsupported platform builds no core and offers no tools. A signed-out account
    /// still yields a provider whose per-session state is unavailable, so the tool host offers it nothing.
    /// </summary>
    /// <param name="options">Platform, state directory, account gate and optional broker, prompt, registrar, environment reader and clock.</param>
    /// <param name="cancellationToken">Token that cancels settings, catalogue and hotkey registration.</param>
    /// <returns>The backend, adapters, core, indicator and disposable owners. Supplied settings, broker, prompt and registrar remain borrowed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The <c>Cowork__ComputerUse__</c> variables or saved settings fail validation. The message lists option keys and never values.</exception>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the kill-trip holder and the wired core move into the result's owned set on success and are disposed in the finally otherwise.")]
    public static async ValueTask<ComputerUseHeadResult> CreateAsync(ComputerUseHeadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var backend = ChooseBackend(options.Platform);
        var status = new ComputerStatusAdapter(backend);
        var access = new ComputerAccessFactsAdapter(options.AccountGate);
        var trip = new KillTrip();
        FileComputerUseSettingsStore? ownedSettings = null;
        WiredCore? wired = null;
        var transferred = false;
        try
        {
            // Same contract as Build: Chrome registers before the default store is built. The entry trips through the
            // holder, whose target is set once the core exists; an unsupported platform adds no kill entry at all.
            if (options.Platform != ComputerPlatform.Unsupported)
            {
                options.Chrome?.Add(new SurfaceChromeEntry(
                    "computer-use-indicator",
                    "Computer",
                    trip.Trip,
                    "kill"));
            }

            IComputerUseSettingsStore settings = options.Settings
                ?? (ownedSettings = new FileComputerUseSettingsStore(options.StateDirectory));
            if (options.Platform != ComputerPlatform.Unsupported)
            {
                wired = await WireCoreAsync(options, backend, access, settings, trip, cancellationToken).ConfigureAwait(false);
            }

            // The trip holder disposes first (the result disposes in reverse), so the chrome Stop can never trip a disposed coordinator.
            IDisposable[] owned = wired is null ? [trip] : [.. wired.Owned, trip];
            var result = new ComputerUseHeadResult(backend, status, access, settings, ownedSettings, wired?.Core, wired?.Tools, wired?.Indicator, owned);
            PublishSettingsFactory(result);
            ownedSettings = null;
            transferred = true;
            return result;
        }
        finally
        {
            if (!transferred)
            {
                wired?.Dispose();
                trip.Dispose();
            }

            ownedSettings?.Dispose();
        }
    }

    private static async ValueTask<WiredCore> WireCoreAsync(ComputerUseHeadOptions options, IComputerUseBackend backend, ComputerAccessFactsAdapter access, IComputerUseSettingsStore settings, KillTrip trip, CancellationToken cancellationToken)
    {
        var time = options.Time ?? TimeProvider.System;
        IGlobalHotkeyRegistrar registrar = options.Hotkey ?? ChooseRegistrar(options.Platform);
        IDisposable? ownedRegistrar = options.Hotkey is null ? registrar as IDisposable : null;
        PressedSubscription? pressedSubscription = null;
        ComputerUseCore? core = null;
        ComputerIndicatorViewModel? indicator = null;
        try
        {
            pressedSubscription = SubscribePressed(registrar, trip);
            core = await ComputerUseComposition.CreateAsync(
                new ComputerUseCompositionOptions
                {
                    GetVariable = options.GetVariable ?? Environment.GetEnvironmentVariable,
                    Settings = settings,
                    Backend = backend,
                    Clipboard = new BackendClipboard(backend),
                    Prompt = options.Prompt ?? UnavailableApprovalPrompt.Instance,
                    Hotkey = registrar,
                    Access = access,
                    Time = time,
                    StateDirectory = options.StateDirectory,
                },
                cancellationToken).ConfigureAwait(false);
            trip.Target = core.KillSwitchCoordinator;
            indicator = new ComputerIndicatorViewModel(core.KillSwitch, "Computer Use", core.Options.KillChord);
            bool armed = core.Options.Enabled
                && await core.KillSwitchCoordinator.TryArmAsync(core.Options.KillChord, cancellationToken).ConfigureAwait(false);
            ComputerUseTools? tools = armed
                ? ComputerUseToolsComposition.AddTools(core, new ComputerUseToolsOptions { Time = time, Backend = backend, Broker = options.Broker ?? new CoworkApprovalBroker(windowAvailable: false), Access = access })
                : null;
            var owned = new List<IDisposable> { indicator, core.KillSwitchCoordinator };
            if (pressedSubscription is not null)
            {
                owned.Add(pressedSubscription);
                pressedSubscription = null;
            }

            if (ownedRegistrar is not null)
            {
                owned.Add(ownedRegistrar);
                ownedRegistrar = null;
            }

            var wired = new WiredCore(core, tools, indicator, [.. owned]);
            core = null;
            indicator = null;
            return wired;
        }
        finally
        {
            if (core is not null)
            {
                indicator?.Dispose();
                core.KillSwitchCoordinator.Dispose();
            }

            pressedSubscription?.Dispose();
            ownedRegistrar?.Dispose();
        }
    }

    /// <summary>A wired core awaiting transfer into a <see cref="ComputerUseHeadResult"/>; disposing it releases everything it owns.</summary>
    private sealed class WiredCore(ComputerUseCore core, ComputerUseTools? tools, ComputerIndicatorViewModel indicator, IDisposable[] owned) : IDisposable
    {
        /// <summary>Gets the gated core.</summary>
        public ComputerUseCore Core { get; } = core;

        /// <summary>Gets the wired tools, or null when the kill switch did not arm or Computer Use is disabled.</summary>
        public ComputerUseTools? Tools { get; } = tools;

        /// <summary>Gets the indicator model over the core kill switch.</summary>
        public ComputerIndicatorViewModel Indicator { get; } = indicator;

        /// <summary>Gets the disposables whose ownership moves to the result.</summary>
        public IDisposable[] Owned { get; } = owned;

        /// <summary>Disposes the owned set in reverse construction order.</summary>
        public void Dispose()
        {
            for (int i = Owned.Length - 1; i >= 0; i--)
            {
                Owned[i].Dispose();
            }
        }
    }

    /// <summary>Chooses the kill-chord registrar. Unsupported platforms get a registrar that never confirms, so arming always fails closed.</summary>
    /// <param name="platform">The head platform.</param>
    /// <returns>The registrar.</returns>
    public static IGlobalHotkeyRegistrar ChooseRegistrar(ComputerPlatform platform) => platform switch
    {
        ComputerPlatform.X11 => new X11HotkeyRegistrar(new X11BackendOptions()),
        ComputerPlatform.Windows => new WindowsHotkeyRegistrar(),
        ComputerPlatform.MacOS => new MacOSHotkeyRegistrar(),
        _ => UnavailableHotkeyRegistrar.Instance,
    };

    private static PressedSubscription? SubscribePressed(IGlobalHotkeyRegistrar registrar, KillTrip trip) => registrar switch
    {
        X11HotkeyRegistrar x11 => new PressedSubscription(handler => x11.Pressed += handler, handler => x11.Pressed -= handler, trip),
        WindowsHotkeyRegistrar windows => new PressedSubscription(handler => windows.Pressed += handler, handler => windows.Pressed -= handler, trip),
        MacOSHotkeyRegistrar macOS => new PressedSubscription(handler => macOS.Pressed += handler, handler => macOS.Pressed -= handler, trip),
        _ => null,
    };

    /// <summary>
    /// The chrome Stop target, filled in once the core exists. Tripping releases held input, revokes the session and
    /// cancels the turn through <see cref="KillSwitchCoordinator.TripAsync"/>; it is never a display-only callback.
    /// </summary>
    private sealed class KillTrip : IDisposable
    {
        /// <summary>Gets or sets the coordinator to trip, or null before the core exists or after it is gone.</summary>
        public IKillSwitchControl? Target { get; set; }

        /// <summary>Trips the current target, if any. The trip is fire-and-forget from a UI command, matching the browser Stop wiring.</summary>
        public void Trip()
        {
            if (Target is { } target)
            {
                _ = target.TripAsync(CancellationToken.None).AsTask();
            }
        }

        /// <summary>Clears the target so a late Stop does nothing.</summary>
        public void Dispose() => Target = null;
    }

    private sealed class PressedSubscription : IDisposable
    {
        private readonly Action<EventHandler> _unsubscribe;
        private readonly EventHandler _handler;
        private int _disposed;

        public PressedSubscription(Action<EventHandler> subscribe, Action<EventHandler> unsubscribe, KillTrip trip)
        {
            _unsubscribe = unsubscribe;
            _handler = (_, _) => trip.Trip();
            subscribe(_handler);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _unsubscribe(_handler);
            }
        }
    }

    /// <summary>Clipboard over the backend's own clipboard primitives. The gate never reads clipboard contents through this port.</summary>
    private sealed class BackendClipboard(IComputerUseBackend backend) : IClipboardTextPort
    {
        /// <inheritdoc />
        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken) => backend.ReadClipboardTextAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask WriteAsync(string text, CancellationToken cancellationToken) => backend.WriteClipboardTextAsync(text, cancellationToken);
    }

    /// <summary>
    /// Fail-closed approval prompt used until the head can show the Computer Use approval dialog. It reports no local
    /// approver, so the policy gate denies every approval-gated action with <see cref="DenialCode.NoLocalApprover"/>
    /// instead of waiting for an answer nothing can give.
    /// </summary>
    private sealed class UnavailableApprovalPrompt : IComputerApprovalPrompt, ILocalApproverAvailability
    {
        /// <summary>The single instance. It holds no state.</summary>
        public static readonly UnavailableApprovalPrompt Instance = new();

        /// <inheritdoc />
        public bool HasLocalApprover => false;

        /// <inheritdoc />
        public ValueTask<ComputerApprovalChoice> RequestAsync(ComputerApprovalFacts facts, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(facts);
            return ValueTask.FromResult(ComputerApprovalChoice.Deny);
        }
    }

    private sealed class UnavailableHotkeyRegistrar : IGlobalHotkeyRegistrar
    {
        /// <summary>The single instance. It holds no state.</summary>
        public static readonly UnavailableHotkeyRegistrar Instance = new();

        /// <inheritdoc />
        public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chord);
            return ValueTask.FromResult(false);
        }

        /// <inheritdoc />
        public ValueTask UnregisterAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
    /// <summary>Revokes only the factory published by <paramref name="owner"/>, skipping disposed predecessors.</summary>
    /// <param name="owner">The result relinquishing its own publication.</param>
    internal static void ReleaseSettingsFactory(ComputerUseHeadResult owner)
    {
        while (true)
        {
            var previous = owner.PreviousSettingsFactory;
            while (previous?.Target is ComputerUseHeadResult previousOwner
                && ReferenceEquals(previous, previousOwner.SettingsFactory)
                && previousOwner.IsDisposed)
            {
                previous = previousOwner.PreviousSettingsFactory;
            }

            var removed = Interlocked.CompareExchange(ref _settingsFactory, previous, owner.SettingsFactory);
            if (!ReferenceEquals(removed, owner.SettingsFactory)
                || previous?.Target is not ComputerUseHeadResult restoredOwner
                || !ReferenceEquals(previous, restoredOwner.SettingsFactory)
                || !restoredOwner.IsDisposed)
            {
                return;
            }

            // A predecessor can finish disposal between the traversal and the exchange. Revoke only its own identity.
            owner = restoredOwner;
        }
    }

    private static void PublishSettingsFactory(ComputerUseHeadResult owner)
    {
        Func<ComputerUseSettingsPage?>? previous;
        do
        {
            previous = SettingsFactory;
            owner.PreviousSettingsFactory = previous;
        }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref _settingsFactory, owner.SettingsFactory, previous), previous));
    }
}
