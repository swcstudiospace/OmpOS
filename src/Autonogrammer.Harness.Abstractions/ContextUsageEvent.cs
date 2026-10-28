namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// The engine reported how much of the model's context window the conversation uses. It is session state, not transcript content: a consumer
/// feeds a context meter and shows nothing in the thread. omp sends it at the end of each turn (<c>usage_update</c>).
/// </summary>
/// <param name="Used">The tokens the conversation uses now.</param>
/// <param name="Size">The size of the context window in tokens. Zero when the engine did not report it.</param>
public sealed record ContextUsageEvent(long Used, long Size) : HarnessEvent;
