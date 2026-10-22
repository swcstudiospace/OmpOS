namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// A consent gate that approves only the hooks a test grants. It can be made to throw, as a failing gate would.
/// </summary>
internal sealed class FakeConsentGate : IAutomationConsentGate
{
    private readonly HashSet<string> _granted = new(StringComparer.Ordinal);

    /// <summary>
    /// True makes every check throw, as a gate that cannot answer would.
    /// </summary>
    public bool Throws { get; set; }

    /// <summary>
    /// Grants consent for a hook.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>This gate, for chaining.</returns>
    public FakeConsentGate Grant(string hookId)
    {
        _granted.Add(hookId);
        return this;
    }

    /// <inheritdoc />
    public bool IsConsented(string hookId)
    {
        if (Throws)
        {
            throw new InvalidOperationException("the consent gate is unavailable");
        }

        return _granted.Contains(hookId);
    }
}
