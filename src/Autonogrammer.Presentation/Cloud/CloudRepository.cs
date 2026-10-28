namespace Autonogrammer.Presentation.Cloud;

/// <summary>A repository a cloud agent works on: an https address that has been validated by <see cref="CloudInputValidator"/>.</summary>
/// <param name="Address">The normalised https address, with no credentials, query or fragment.</param>
public sealed record CloudRepository(string Address)
{
    /// <summary>Gets the short name <c>owner/repository</c> taken from the address, without a trailing <c>.git</c>.</summary>
    public string DisplayName => CloudInputValidator.DisplayNameOf(Address);
}
