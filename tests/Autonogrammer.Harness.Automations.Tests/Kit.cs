namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>
/// Shared fixtures: a fixed start instant, automation builders and engine construction.
/// </summary>
internal static class Kit
{
    /// <summary>The fake clock's start instant.</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The consent hook for an automation. Each automation built by <see cref="Define"/> is gated by this hook.
    /// </summary>
    /// <param name="automationId">The automation identifier.</param>
    public static string HookFor(string automationId) => $"{automationId}#0";

    /// <summary>
    /// Builds an automation gated by <see cref="HookFor"/>.
    /// </summary>
    public static AutomationDefinition Define(
        string automationId,
        AutomationTrigger trigger,
        AutomationBody body,
        TimeSpan? maxRuntime = null,
        RateLimit? rateLimit = null) =>
        new(automationId, trigger, HookFor(automationId), maxRuntime ?? TimeSpan.FromMinutes(5), body, rateLimit);

    /// <summary>
    /// A consent gate that grants the hook of each named automation.
    /// </summary>
    public static FakeConsentGate ConsentFor(params string[] automationIds)
    {
        var gate = new FakeConsentGate();
        foreach (var id in automationIds)
        {
            gate.Grant(HookFor(id));
        }

        return gate;
    }

    /// <summary>
    /// A body that counts its runs and completes.
    /// </summary>
    public static AutomationBody Counting(Tally tally) =>
        (_, _) =>
        {
            tally.Count++;
            return Task.CompletedTask;
        };

    /// <summary>
    /// A body that counts its runs and emits the named event, so the next automation in a chain runs.
    /// </summary>
    public static AutomationBody EmitThen(string eventName, Tally tally) =>
        async (context, cancellationToken) =>
        {
            tally.Count++;
            await context.EmitAsync(eventName, cancellationToken).ConfigureAwait(false);
        };

    /// <summary>
    /// Builds an engine with the given clock, consent gate and store.
    /// </summary>
    public static AutomationEngine Engine(
        TimeProvider clock,
        IAutomationConsentGate consent,
        IAutomationGuardStore guards,
        params AutomationDefinition[] definitions) =>
        new(definitions, consent, guards, clock);
}
