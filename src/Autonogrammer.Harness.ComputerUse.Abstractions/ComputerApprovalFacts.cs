namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Host-built approval facts. There is no model-text field. The 160 by 120 crop and the 40-character preview are host-produced.
/// The dialog times out to deny after <see cref="Timeout"/> (120 seconds). A remote approval is not accepted.
/// </summary>
public sealed record ComputerApprovalFacts
{
    /// <summary>How long the dialog waits before it denies.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    /// <summary>Application display name observed by the host.</summary>
    public required string ApplicationName { get; init; }

    /// <summary>Application identity observed by the host.</summary>
    public required string AppIdentity { get; init; }

    /// <summary>Window title sanitised and quoted, at most 80 characters.</summary>
    public required string SanitizedTitle { get; init; }

    /// <summary>The action in host words.</summary>
    public required string HostActionWords { get; init; }

    /// <summary>Host-sanitised preview (character count plus at most 40 characters), labelled as agent text by the dialog. Not a model-text field.</summary>
    public required string HostSanitizedPreview { get; init; }

    /// <summary>Local or Dispatch.</summary>
    public required ComputerActionOrigin Origin { get; init; }

    /// <summary>Controller device name when <see cref="Origin"/> is Dispatch.</summary>
    public string? DeviceName { get; init; }

    /// <summary>The user's security phrase. Own-process windows are masked so the model cannot read it.</summary>
    public string? SecurityPhrase { get; init; }
}

