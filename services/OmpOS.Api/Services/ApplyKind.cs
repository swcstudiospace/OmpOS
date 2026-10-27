namespace OmpOS.Api.Services;

/// <summary>What applying a change decision in the workspace did.</summary>
internal enum ApplyKind
{
    /// <summary>The API has no workspace folder; the decision is recorded and nothing was written.</summary>
    Recorded,

    /// <summary>The workspace holds the decided text, because the decision wrote it or it was already there.</summary>
    Applied,

    /// <summary>The file in the workspace holds neither the text before nor the text after the edit, so nothing was written.</summary>
    Conflict,

    /// <summary>The reported path resolves outside the workspace folder or through a link, so nothing was written.</summary>
    Outside,

    /// <summary>The file could not be read or written.</summary>
    Failed,
}
