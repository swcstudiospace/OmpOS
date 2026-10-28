namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// How OmpOS decides whether a tool call the agent proposes runs without asking, using Cursor's Run Mode names. omp has no hosted
/// classifier model, so the Auto-review classifier is omitted by decision: what each mode means in OmpOS is stated on every member.
/// </summary>
public enum RunMode
{
    /// <summary>
    /// The default. A call that matches an allow rule runs; a read-only inspection from a short built-in list of known-safe commands
    /// runs; a call that matches a deny rule is refused; every other call asks. Cursor's classifier is not available in OmpOS, so a call
    /// it would have reviewed asks you instead.
    /// </summary>
    AutoReview = 0,

    /// <summary>A call that matches an allow rule runs, a call that matches a deny rule is refused, and every other call asks.</summary>
    Allowlist = 1,

    /// <summary>Every call runs without asking. A deny rule is applied only where omp asks OmpOS about the call, and omp does not ask in this mode.</summary>
    RunEverything = 2,
}
