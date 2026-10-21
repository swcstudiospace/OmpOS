namespace OmpOS.Contracts.Ui;

/// <summary>
/// A fenced code block (kind "code").
/// </summary>
public sealed record CodeBlock : UiBlock
{
    /// <summary>Gets the programming language tag.</summary>
    public required string Language { get; init; }

    /// <summary>Gets the code text.</summary>
    public required string Code { get; init; }
}
