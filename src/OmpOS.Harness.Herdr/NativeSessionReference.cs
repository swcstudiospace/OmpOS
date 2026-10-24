namespace OmpOS.Harness.Herdr;

/// <summary>
/// The omp session reference that the herdr integration reports for a session. The contract treats it as opaque: it is
/// stored and passed back to resume a conversation, and it is never parsed. Its text is never written by
/// <see cref="ToString"/>, so a log line or debugger view of a record that carries it shows only a marker.
/// </summary>
public sealed record NativeSessionReference
{
    /// <summary>Creates a native session reference.</summary>
    /// <param name="value">The opaque reference text.</param>
    public NativeSessionReference(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque reference text.</summary>
    public string Value { get; }

    /// <summary>Returns a fixed redaction marker. The reference text is not included.</summary>
    public override string ToString() => "NativeSessionReference { Value = <redacted> }";
}
