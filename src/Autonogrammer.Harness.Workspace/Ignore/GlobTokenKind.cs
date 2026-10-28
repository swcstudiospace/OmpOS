namespace Autonogrammer.Workspace.Ignore;

/// <summary>The kinds of element a compiled glob segment is made of.</summary>
internal enum GlobTokenKind
{
    /// <summary>One literal character.</summary>
    Literal,

    /// <summary>Any single character (<c>?</c>).</summary>
    AnyCharacter,

    /// <summary>Any run of characters, including none (<c>*</c>).</summary>
    Star,

    /// <summary>One character from a set or range list (<c>[abc]</c>, <c>[a-z]</c>, <c>[!abc]</c>).</summary>
    CharacterClass,
}
