namespace OmpOS.Native.Views.Blocks;

/// <summary>What a line of a drawn diff is: unchanged context, a line only the new text has, or a line only the old text had.</summary>
internal enum DiffEntryKind
{
    /// <summary>The line is in both texts.</summary>
    Context,

    /// <summary>The line is only in the new text.</summary>
    Added,

    /// <summary>The line is only in the old text.</summary>
    Removed,
}
