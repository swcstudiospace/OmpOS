namespace OmpOS.Presentation.Chats;

/// <summary>
/// One match of a transcript search. <see cref="BlockIndex"/> indexes <c>UiTree.Blocks</c>; the index equal to the block
/// count names the streaming tail, which is not a block yet. <see cref="Offset"/> is a character offset into that block's
/// searchable text, and <see cref="Length"/> is the matched length in characters.
/// </summary>
/// <remarks>
/// A match in a side chat sets <see cref="ChatId"/> and <see cref="ChatTitle"/>. For it, <see cref="BlockIndex"/> indexes the
/// side chat's <see cref="SideChat.Messages"/> and <see cref="Offset"/> is a character offset into that message's text.
/// A match in the parent transcript leaves both null.
/// </remarks>
/// <param name="BlockIndex">The block index in the transcript, or the message index in the side chat.</param>
/// <param name="Offset">The character offset of the match in the block's or message's searchable text.</param>
/// <param name="Length">The matched length in characters.</param>
/// <param name="ChatId">The identifier of the side chat holding the match, or null for the parent transcript.</param>
/// <param name="ChatTitle">The title of the side chat holding the match, or null for the parent transcript.</param>
public sealed record TranscriptMatch(int BlockIndex, int Offset, int Length, string? ChatId = null, string? ChatTitle = null)
{
    /// <summary>Gets whether the match is in a side chat rather than the parent transcript.</summary>
    public bool IsInSideChat => ChatId is not null;

    /// <summary>Gets the name of where the match is: the side chat's title, or "Transcript" for the parent transcript.</summary>
    public string LocationName => ChatTitle ?? "Transcript";

    /// <summary>
    /// Gets a one-line description of the match for a result list: where it is and its position there, such as
    /// "Side chat 1 - message 2" or "Transcript - block 3". Positions are one-based.
    /// </summary>
    public string Label => $"{LocationName} - {(IsInSideChat ? "message" : "block")} {BlockIndex + 1}";
}
