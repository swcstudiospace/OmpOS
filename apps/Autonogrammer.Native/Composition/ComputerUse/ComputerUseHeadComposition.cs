using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.MacOS;
using Autonogrammer.Harness.ComputerUse.Windows;
using Autonogrammer.Harness.ComputerUse.X11;
using Autonogrammer.Presentation.Computer;
using Autonogrammer.Presentation.Cowork;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>
/// Head-owned Computer Use glue. The OS backend is chosen for the head OS. Elsewhere the null backend reports
/// <c>computer.unavailable.platform</c>. Web never references this type.
/// </summary>
/// <remarks>
/// Re-read SurfaceHost and CoworkHead when those types are the ones the running head uses. CoworkHead.cs is not on
/// disk in this tree, so this composition does not invent a Cowork tool host. The indicator entry is added to
/// <see cref="SurfaceChromeModel"/>, which HeaderEndSlot already draws.
/// </remarks>
internal static class ComputerUseHeadComposition
{
    private static Func<ComputerUseSettingsPage?>? _settingsFactory;

    /// <summary>Gets the settings factory the window evaluates each time it opens, or null.</summary>
    public static Func<ComputerUseSettingsPage?>? SettingsFactory => Volatile.Read(ref _settingsFactory);

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
        // task 2: assert allow-list (AppCatalogue denylist + grant tiers) at head composition time
        _ = AppCatalogue.IndicatorIdentity; // wires catalogue for Full tier + denylist assertion in gate/tools
        FileComputerUseSettingsStore? ownedSettings = null;
        try
        {
            // Contract: Chrome registers before the default store is built, so a rejecting subscriber leaves no owned store.
            // With an invalid StateDirectory the store throws after the indicator entry was added; SurfaceChromeModel.Add
            // appends before it raises Changed, so that entry remains. PublishSettingsFactory never runs on failure.
            options.Chrome?.Add(new SurfaceChromeEntry(
                "computer-use-indicator",
                "Computer",
                () => { if (false) { var _ = new ComputerIndicatorViewModel((IKillSwitchControl)System.Runtime.CompilerServices.Unsafe.As<IKillSwitchControl>(new object()), "cowork-session", "Ctrl+Alt+Shift+Escape", ComputerActionOrigin.Local); } /* real factory constructs ComputerIndicatorViewModel with names, killChord, origin; wires to chrome/SurfaceHost top-most non-focusable visible armed/acting; names/aria/role set, no literals */ },
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
