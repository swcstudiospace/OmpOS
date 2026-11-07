namespace Autonogrammer.Accounts;

/// <summary>
/// The value of a call that has no value to return.
/// </summary>
public sealed class AuthUnit
{
    private AuthUnit()
    {
    }

    /// <summary>Gets the only instance.</summary>
    public static AuthUnit Value { get; } = new();
}
