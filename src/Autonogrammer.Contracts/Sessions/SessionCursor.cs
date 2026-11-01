namespace Autonogrammer.Contracts.Sessions;

/// <summary>
/// The keyset position of one session in a listing ordered newest first by <see cref="Session.UpdatedAt"/>, then by
/// <see cref="Session.Id"/> in descending ordinal order. A listing that is given the cursor of the last session of the previous
/// page continues strictly after it, so a page is stable while sessions are created or updated between two calls.
/// </summary>
/// <param name="UpdatedAt">The <see cref="Session.UpdatedAt"/> of the last session of the previous page.</param>
/// <param name="Id">The <see cref="Session.Id"/> of the last session of the previous page.</param>
public sealed record SessionCursor(DateTimeOffset UpdatedAt, string Id);
