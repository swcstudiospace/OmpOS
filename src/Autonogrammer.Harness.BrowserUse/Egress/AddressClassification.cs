namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>The result of classifying one address or literal.</summary>
/// <param name="Class">Whether the address is allowed, denied, or ambiguous.</param>
/// <param name="Reason">A stable reason token. It never contains a secret.</param>
public readonly record struct AddressClassification(AddressClass Class, string Reason)
{
    /// <summary>Gets a value indicating whether the address must not be dialed.</summary>
    public bool IsRefused => Class != AddressClass.Allowed;
}
