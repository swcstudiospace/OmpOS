namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Evaluates a prompt hook with one model call. The implementation lives with the provider settings (a later unit); the
/// dispatcher only calls it. While the call runs, hooks are disabled for every dispatch in its async flow, so a prompt hook
/// cannot trigger itself.
/// </summary>
public interface IPromptHookEvaluator
{
    /// <summary>
    /// Asks the model to judge the prompt and answer <c>{ ok, reason? }</c>.
    /// </summary>
    /// <param name="prompt">The prompt with <c>$ARGUMENTS</c> already replaced by the input JSON.</param>
    /// <param name="model">The hook's model override, or null for the default.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer. An exception or no answer counts as a crash of the hook.</returns>
    ValueTask<PromptHookAnswer> EvaluateAsync(string prompt, string? model, CancellationToken cancellationToken);
}
