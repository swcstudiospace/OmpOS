using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>Dependencies the head passes. There is no container registration.</summary>
public sealed class ComputerUseCompositionOptions
{
    /// <summary>Environment reader. <see langword="null"/> means the variable is absent.</summary>
    public required Func<string, string?> GetVariable { get; init; }

    /// <summary>Per-user settings store.</summary>
    public required IComputerUseSettingsStore Settings { get; init; }

    /// <summary>Caller-supplied backend. This composition does not construct an OS backend.</summary>
    public required IComputerUseBackend Backend { get; init; }

    /// <summary>Head clipboard port.</summary>
    public required IClipboardTextPort Clipboard { get; init; }

    /// <summary>Local approval prompt.</summary>
    public required IComputerApprovalPrompt Prompt { get; init; }

    /// <summary>Kill-chord registrar.</summary>
    public required IGlobalHotkeyRegistrar Hotkey { get; init; }

    /// <summary>Account facts.</summary>
    public required IComputerUseAccessFacts Access { get; init; }

    /// <summary>Clock.</summary>
    public required TimeProvider Time { get; init; }

    /// <summary>State directory for later audit files.</summary>
    public required string StateDirectory { get; init; }
}
