namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>One frame the model may see. At most one image is attached to a result that carries this record.</summary>
public sealed record FrameRecord
{
    /// <summary>Frame id.</summary>
    public required string Id { get; init; }

    /// <summary>Display id.</summary>
    public required string DisplayId { get; init; }

    /// <summary>Native width.</summary>
    public required int NativeWidth { get; init; }

    /// <summary>Native height.</summary>
    public required int NativeHeight { get; init; }

    /// <summary>Scaled width.</summary>
    public required int ScaledWidth { get; init; }

    /// <summary>Scaled height.</summary>
    public required int ScaledHeight { get; init; }

    /// <summary>Scale factor from <see cref="Abstractions.CoordinateMap"/>.</summary>
    public required double ScaleFactor { get; init; }

    /// <summary>Cursor text in model coordinates.</summary>
    public required string Cursor { get; init; }

    /// <summary>SHA-256 of the PNG, lowercase hex.</summary>
    public required string Sha256 { get; init; }

    /// <summary>PNG bytes. One image.</summary>
    public required ReadOnlyMemory<byte> Png { get; init; }

    /// <summary>Capture time.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Session frame number, starting at 1.</summary>
    public required int Number { get; init; }

    /// <summary>Hint when three hashes match, otherwise null.</summary>
    public string? IdenticalHashHint { get; init; }
}
