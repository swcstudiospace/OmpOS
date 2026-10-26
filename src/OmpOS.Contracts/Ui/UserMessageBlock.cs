namespace OmpOS.Contracts.Ui;

/// <summary>
/// A user turn folded from a <c>prompt_accepted</c> or <c>user_message</c> event: the prompt text in event order,
/// rendered before the assistant output of the same turn. This block only exists in folded
/// trees; it is never produced by deserialization (the wire kind <c>user</c> degrades to
/// <see cref="UnknownBlock"/>), but a folded tree serializes it under that kind.
/// </summary>
public sealed record UserMessageBlock : UiBlock
{
    /// <summary>Gets the role marker carried by every user turn.</summary>
    public static string Role => "user";

    /// <summary>Gets the verbatim prompt text.</summary>
    public required string Text { get; init; }
}
