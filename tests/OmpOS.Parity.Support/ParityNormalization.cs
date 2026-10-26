using OmpOS.Contracts.Ui;
using OmpOS.GenUi;

namespace OmpOS.Parity.Support;

/// <summary>
/// The normalization contract every head implements so that one shared expectation can be checked against
/// each rendering.
/// </summary>
/// <remarks>
/// <para>Kinds: a fold block maps to a kind through <see cref="KindOf"/>; a rendered block maps to the kind
/// of its root class (see <see cref="ParityKinds"/>). The streaming tail is kind <see cref="ParityKinds.Streaming"/>
/// and follows every flushed block.</para>
/// <para>Visible text: a head reads the rendered block as text, where every element boundary contributes one
/// space, descendants carrying a non-empty <c>aria-label</c> are controls and contribute nothing, and then
/// <see cref="CollapseWhitespace"/> is applied. Scenarios therefore never place inline formatting inside a word.</para>
/// </remarks>
public static class ParityNormalization
{
    /// <summary>Collapses every run of whitespace to one space and trims both ends.</summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The normalized text; empty when the input has no visible characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static string CollapseWhitespace(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Maps one folded block to its normalized kind.</summary>
    /// <param name="block">The folded block.</param>
    /// <returns>The kind from <see cref="ParityKinds"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="block"/> is a type the contract does not cover.</exception>
    public static string KindOf(UiBlock block) => block switch
    {
        UserMessageBlock => ParityKinds.User,
        MarkdownBlock => ParityKinds.Markdown,
        CodeBlock => ParityKinds.Code,
        ToolCallBlock => ParityKinds.ToolCall,
        PermissionBlock => ParityKinds.Permission,
        PlanBlock => ParityKinds.Plan,
        DiffPlaceholderBlock => ParityKinds.Diff,
        ErrorBlock => ParityKinds.Error,
        A2uiSurfaceBlock => ParityKinds.A2ui,
        UnknownBlock => ParityKinds.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, "No parity kind is defined for this block type."),
    };

    /// <summary>Lists the kinds of a folded tree in render order, including the streaming tail when present.</summary>
    /// <param name="tree">The folded tree.</param>
    /// <returns>The kinds, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is null.</exception>
    public static IReadOnlyList<string> KindsOf(UiTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var kinds = tree.Blocks.Select(KindOf).ToList();
        if (tree.StreamingText.Length > 0)
        {
            kinds.Add(ParityKinds.Streaming);
        }

        return kinds;
    }
}
