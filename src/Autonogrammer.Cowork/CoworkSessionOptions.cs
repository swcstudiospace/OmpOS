namespace Autonogrammer.Cowork;

/// <summary>Options for opening a Cowork session registry.</summary>
public sealed record CoworkSessionOptions
{
    /// <summary>Gets the state directory. Cowork files live under <c>cowork/&lt;account&gt;</c> below it, never beside the Code registry.</summary>
    public required string StateRoot { get; init; }
}
