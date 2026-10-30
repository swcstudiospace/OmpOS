
namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Host-built facts beside a request. The model does not fill this record.</summary>
public sealed record ComputerHostAction
{
    /// <summary>Action class.</summary>
    public required ComputerActionClass ActionClass { get; init; }

    /// <summary>Chord, when the action is a chord.</summary>
    public string? Chord { get; init; }

    /// <summary>Host flag for file deletion.</summary>
    public bool IsFileDeletion { get; init; }

    /// <summary><see langword="true"/> for an actuating action. Listing is not actuating.</summary>
    public bool Actuating { get; init; } = true;
}
