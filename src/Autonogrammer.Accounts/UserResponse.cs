namespace Autonogrammer.Accounts;

/// <summary>A user of a wire answer.</summary>
/// <param name="Id">The user identifier.</param>
/// <param name="Email">The address, when present.</param>
internal sealed record UserResponse(string? Id, string? Email);
