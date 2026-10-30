using Autonogrammer.Cowork;
using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.MacOS;
using Autonogrammer.Harness.ComputerUse.Windows;
using Autonogrammer.Harness.ComputerUse.X11;
using Autonogrammer.Presentation.Computer;
using Autonogrammer.Presentation.Cowork;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>What <see cref="ComputerUseHeadComposition.Build"/> needs. No container.</summary>
internal sealed class ComputerUseHeadOptions
{
    /// <summary>Gets the platform the head is running on.</summary>
    public required ComputerPlatform Platform { get; init; }

    /// <summary>Gets the state directory for <c>computer-use/settings.json</c>.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the Cowork account gate. The access-facts adapter reads only this.</summary>
    public required ICoworkAccountGate AccountGate { get; init; }

    /// <summary>Gets the header-end slot model, or null. Phase 69 owns the real SurfaceHost.</summary>
    public SurfaceChromeModel? Chrome { get; init; }
}

/// <summary>What composition built. The settings page is created when the window opens.</summary>
internal sealed class ComputerUseHeadResult
{
    /// <summary>Gets the backend. NullComputerUseBackend when the platform is unsupported.</summary>
    public required IComputerUseBackend Backend { get; init; }

    /// <summary>Gets the status adapter.</summary>
    public required ComputerStatusAdapter Status { get; init; }

    /// <summary>Gets the access-facts adapter.</summary>
    public required ComputerAccessFactsAdapter Access { get; init; }

    /// <summary>Gets the settings file.</summary>
    public required FileComputerUseSettingsStore Settings { get; init; }
}

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
    /// <summary>Gets the settings factory the window evaluates each time it opens, or null.</summary>
    public static Func<ComputerUseSettingsPage?>? SettingsFactory { get; private set; }

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
    /// <returns>The backend, adapters and settings store.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static ComputerUseHeadResult Build(ComputerUseHeadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var backend = ChooseBackend(options.Platform);
        var status = new ComputerStatusAdapter(backend);
        var access = new ComputerAccessFactsAdapter(options.AccountGate);
        var settings = new FileComputerUseSettingsStore(options.StateDirectory);
        options.Chrome?.Add(new SurfaceChromeEntry(
            "computer-use-indicator",
            "Computer",
            static () => { },
            "kill"));
        SettingsFactory = () => null;
        return new ComputerUseHeadResult
        {
            Backend = backend,
            Status = status,
            Access = access,
            Settings = settings,
        };
    }
}
