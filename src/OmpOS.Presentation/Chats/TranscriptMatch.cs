namespace OmpOS.Presentation.Chats;

/// <summary>
/// One match of a transcript search. <see cref="BlockIndex"/> indexes <c>UiTree.Blocks</c>; the index equal to the block
/// count names the streaming tail, which is not a block yet. <see cref="Offset"/> is a character offset into that block's
/// searchable text, and <see cref="Length"/> is the matched length in characters.
/// </summary>
public sealed record TranscriptMatch(int BlockIndex, int Offset, int Length);
