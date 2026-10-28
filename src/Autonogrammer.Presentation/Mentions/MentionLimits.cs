namespace Autonogrammer.Presentation.Mentions;

/// <summary>Size limits of the mention code.</summary>
public static class MentionLimits
{
    /// <summary>The default byte cap of the content of one mention when the message is sent (256 KiB).</summary>
    public const int DefaultContentBytes = 256 * 1024;

    /// <summary>The most mentions one message carries.</summary>
    public const int MaxMentionsPerMessage = 32;

    /// <summary>The most rows the picker lists for one source when the user has typed a query.</summary>
    public const int RowsPerSourceTyped = 8;

    /// <summary>The most rows the picker lists for one source while the query is empty.</summary>
    public const int RowsPerSourceEmpty = 4;

    /// <summary>The most rows the picker lists in all.</summary>
    public const int MaxRows = 40;
}
