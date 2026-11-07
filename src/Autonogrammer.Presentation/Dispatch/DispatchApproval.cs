namespace Autonogrammer.Presentation.Dispatch;

/// <summary>A command waiting for the local user's decision. Its text is plain and capped at two hundred characters (D-29).</summary>
/// <param name="Id">The approval.</param>
/// <param name="Operation">The operation, from the closed set.</param>
/// <param name="Text">The task text, plain and capped.</param>
public sealed record DispatchApproval(string Id, string Operation, string Text)
{
    /// <summary>Creates an approval whose task text is capped and plain.</summary>
    /// <param name="id">The approval.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="text">The task text, which is capped.</param>
    /// <returns>The approval.</returns>
    public static DispatchApproval Create(string id, string operation, string text) =>
        new(id, operation, DispatchText.Plain(text));
}
