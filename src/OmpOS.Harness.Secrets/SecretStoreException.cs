namespace OmpOS.Harness.Secrets;

/// <summary>
/// A secret store could not read or write its backing storage: the file is corrupt, has an unsupported format, or cannot be read
/// or replaced. The message is a plain sentence; it never contains a secret value or any part of the stored contents.
/// </summary>
public sealed class SecretStoreException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SecretStoreException"/> class.</summary>
    public SecretStoreException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SecretStoreException"/> class with a message.</summary>
    /// <param name="message">The sentence that says what failed. It must not contain a secret.</param>
    public SecretStoreException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SecretStoreException"/> class with a message and the failure behind it.</summary>
    /// <param name="message">The sentence that says what failed. It must not contain a secret.</param>
    /// <param name="innerException">The input/output failure behind the message.</param>
    public SecretStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
