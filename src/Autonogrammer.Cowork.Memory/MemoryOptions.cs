namespace Autonogrammer.Cowork.Memory;

/// <summary>Options for <c>Cowork:Memory</c>. Caps are named constants so a refusal can cite them.</summary>
public sealed class MemoryOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:Memory";

    /// <summary>Content cap, in characters.</summary>
    public const int MaxContentChars = 1_000;

    /// <summary>Topic cap, in characters.</summary>
    public const int MaxTopicChars = 120;

    /// <summary>Entries kept per account, including project memory.</summary>
    public const int MaxEntriesPerAccount = 1_000;

    /// <summary>Agent saves allowed per session per hour.</summary>
    public const int MaxSavesPerSessionPerHour = 20;

    /// <summary>Brief cap, in bytes.</summary>
    public const int MaxBriefBytes = 4 * 1024;

    /// <summary>Recall query cap, in characters.</summary>
    public const int MaxQueryChars = 200;

    /// <summary>Gets or sets whether memory is enabled. The default is false.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets whether memory is paused.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets or sets whether sensitive categories may be saved.</summary>
    public bool IncludeSensitive { get; set; }

    /// <summary>Gets or sets the entry cap. The default is <see cref="MaxEntriesPerAccount"/>.</summary>
    public int MaxEntries { get; set; } = MaxEntriesPerAccount;

    /// <summary>Gets or sets the content cap. The default is <see cref="MaxContentChars"/>.</summary>
    public int MaxContent { get; set; } = MaxContentChars;

    /// <summary>Gets or sets the topic cap. The default is <see cref="MaxTopicChars"/>.</summary>
    public int MaxTopic { get; set; } = MaxTopicChars;

    /// <summary>Gets or sets the hourly save cap. The default is <see cref="MaxSavesPerSessionPerHour"/>.</summary>
    public int MaxSavesPerHour { get; set; } = MaxSavesPerSessionPerHour;

    /// <summary>Gets or sets the brief cap. The default is <see cref="MaxBriefBytes"/>.</summary>
    public int BriefBytes { get; set; } = MaxBriefBytes;

    /// <summary>Gets or sets the query cap. The default is <see cref="MaxQueryChars"/>.</summary>
    public int MaxQuery { get; set; } = MaxQueryChars;
}
