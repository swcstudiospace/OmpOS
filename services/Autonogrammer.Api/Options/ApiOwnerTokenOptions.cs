namespace Autonogrammer.Api.Options;

/// <summary>Binds <c>Api:Auth:OwnerToken</c>: the digests of the operator's owner token and the identity it maps to.</summary>
internal sealed class ApiOwnerTokenOptions
{
    /// <summary>Gets the default subject the owner token maps to.</summary>
    public const string DefaultSubject = "owner";

    /// <summary>Gets the default failed attempts a client address may make per minute.</summary>
    public const int DefaultMaxFailuresPerMinute = 10;

    /// <summary>Gets or sets one to three lowercase-hex SHA-256 digests of accepted owner tokens; a second digest supports rotation.</summary>
    public IList<string> Sha256 { get; set; } = [];

    /// <summary>Gets or sets the <c>sub</c> the owner token authenticates as. It owns the sessions the operator creates.</summary>
    public string Subject { get; set; } = DefaultSubject;

    /// <summary>Gets or sets how many failed attempts a client address may make per minute before it receives 429.</summary>
    public int MaxFailuresPerMinute { get; set; } = DefaultMaxFailuresPerMinute;
}
