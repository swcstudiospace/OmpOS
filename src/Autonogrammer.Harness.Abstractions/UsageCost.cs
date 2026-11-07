namespace Autonogrammer.Harness.Abstractions;

/// <summary>A cost the engine reported for a session. It is a number and a currency, never prompt content.</summary>
/// <param name="Amount">The amount in <paramref name="Currency"/>.</param>
/// <param name="Currency">The currency code the engine sent. Not empty.</param>
public sealed record UsageCost(decimal Amount, string Currency)
{
    /// <summary>Creates a cost, refusing a blank currency.</summary>
    /// <param name="amount">The amount in <paramref name="currency"/>.</param>
    /// <param name="currency">The currency code the engine sent.</param>
    /// <returns>The cost.</returns>
    /// <exception cref="ArgumentException"><paramref name="currency"/> is null, empty or white space.</exception>
    public static UsageCost Create(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        return new UsageCost(amount, currency);
    }

    /// <summary>Returns the type name. The amount and currency stay out of logs.</summary>
    public override string ToString() => nameof(UsageCost);
}
