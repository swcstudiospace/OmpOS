namespace OmpOS.Parity.Support;

/// <summary>
/// The normalized block kinds of the session-chat contract. Every head maps its rendered blocks to these
/// names, and the presentation fold maps its <c>UiBlock</c> types to the same names.
/// </summary>
public static class ParityKinds
{
    /// <summary>A user turn folded from <c>prompt_accepted</c>.</summary>
    public const string User = "user";

    /// <summary>A flushed assistant markdown block.</summary>
    public const string Markdown = "markdown";

    /// <summary>A code block with its language label.</summary>
    public const string Code = "code";

    /// <summary>A tool-call card with its name and status.</summary>
    public const string ToolCall = "tool_call";

    /// <summary>A permission request with its read-only options.</summary>
    public const string Permission = "permission";

    /// <summary>A plan panel with its ordered steps.</summary>
    public const string Plan = "plan";

    /// <summary>A diff placeholder with its path and reason.</summary>
    public const string Diff = "diff";

    /// <summary>An error block with its message.</summary>
    public const string Error = "error";

    /// <summary>An A2UI surface with its rendered components.</summary>
    public const string A2ui = "a2ui";

    /// <summary>An unrecognised payload kept as a collapsed disclosure.</summary>
    public const string Unknown = "unknown";

    /// <summary>The streamed text not yet flushed into a markdown block; rendered after all blocks.</summary>
    public const string Streaming = "streaming";
}
