namespace OmpOS.Presentation.Mentions;

/// <summary>The character that opened a mention.</summary>
public enum MentionTrigger
{
    /// <summary><c>@</c>: files, folders, terminals, chats, diffs, the browser, rules, skills and side chats.</summary>
    At = 0,

    /// <summary><c>#</c>: pull requests.</summary>
    Hash = 1,
}
