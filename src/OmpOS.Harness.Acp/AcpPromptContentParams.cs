namespace OmpOS.Harness.Acp;

/// <summary>Parameters of <c>session/prompt</c> with embedded resources; the wire shape equals the text-only prompt parameters.</summary>
/// <param name="SessionId">The session the prompt belongs to.</param>
/// <param name="Prompt">The prompt content blocks.</param>
public sealed record AcpPromptContentParams(string SessionId, IReadOnlyList<AcpPromptBlock> Prompt);
