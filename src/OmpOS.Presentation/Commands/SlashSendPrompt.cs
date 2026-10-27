namespace OmpOS.Presentation.Commands;

/// <summary>The text is sent to the engine, as typed or rewritten: the raw command for omp, an expanded template, a skill hand-off or a subagent hand-off.</summary>
/// <param name="Prompt">The prompt text to send.</param>
/// <param name="Chosen">The source that was chosen.</param>
/// <param name="Alternatives">The other sources that share the name and were not chosen, each with its source label; empty when the name is unambiguous.</param>
public sealed record SlashSendPrompt(string Prompt, SlashCandidate Chosen, IReadOnlyList<SlashCandidate> Alternatives) : SlashResolution;
