using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Cowork;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>What <see cref="ComputerUseHeadComposition.CreateAsync"/> needs. No container. The synchronous <see cref="ComputerUseHeadComposition.Build"/> reads only platform, state directory, settings, account gate and chrome.</summary>
internal sealed class ComputerUseHeadOptions
{
    /// <summary>Gets the platform the head is running on.</summary>
    public required ComputerPlatform Platform { get; init; }

    /// <summary>Gets the state directory for <c>computer-use/settings.json</c>.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>
    /// Gets a borrowed settings store, or null to create a file store owned by the result. A supplied store remains
    /// caller-owned, even when it implements <see cref="IDisposable"/>.
    /// </summary>
    public IComputerUseSettingsStore? Settings { get; init; }

    /// <summary>Gets the Cowork account gate. The access-facts adapter reads only this.</summary>
    public required ICoworkAccountGate AccountGate { get; init; }

    /// <summary>
    /// Gets the local approval broker shared with the Cowork tool host, or null for a fail-closed broker that
    /// reports unavailable until a desktop window can show the ask. A supplied broker remains caller-owned.
    /// </summary>
    public ICoworkApprovalBroker? Broker { get; init; }

    /// <summary>
    /// Gets the Computer Use approval prompt for the policy gate, or null for the fail-closed prompt that denies
    /// every gated approval with no local approver. A supplied prompt remains caller-owned.
    /// </summary>
    public IComputerApprovalPrompt? Prompt { get; init; }

    /// <summary>
    /// Gets a borrowed kill-chord registrar, or null to create the platform registrar owned by the result.
    /// A supplied registrar remains caller-owned, even when it implements <see cref="IDisposable"/>.
    /// </summary>
    public IGlobalHotkeyRegistrar? Hotkey { get; init; }

    /// <summary>Gets the environment reader for <c>Cowork__ComputerUse__</c> variables. Null reads the process environment.</summary>
    public Func<string, string?>? GetVariable { get; init; }

    /// <summary>Gets the clock. Null uses <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? Time { get; init; }

    /// <summary>Gets the header-end slot model, or null. Phase 69 owns the real SurfaceHost.</summary>
    public SurfaceChromeModel? Chrome { get; init; }
}
