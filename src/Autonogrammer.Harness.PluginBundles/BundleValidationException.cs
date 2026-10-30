namespace Autonogrammer.Harness.PluginBundles;

/// <summary>A bundle was refused. The message is the fixed <c>BundleInvalid(code)</c> string and never includes file contents.</summary>
public sealed class BundleValidationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BundleValidationException"/> class for a malformed manifest.</summary>
    public BundleValidationException()
        : this(BundleInvalidCode.BundleBadManifest)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BundleValidationException"/> class.</summary>
    /// <param name="message">The message.</param>
    public BundleValidationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BundleValidationException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BundleValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BundleValidationException"/> class.</summary>
    /// <param name="code">The named reason.</param>
    /// <param name="relativePath">The offending path relative to the bundle, or <see langword="null"/>.</param>
    public BundleValidationException(BundleInvalidCode code, string? relativePath = null)
        : base("BundleInvalid(" + code + ")")
    {
        Code = code;
        RelativePath = relativePath;
    }

    /// <summary>Gets the named reason.</summary>
    public BundleInvalidCode Code { get; }

    /// <summary>Gets the offending path relative to the bundle, or <see langword="null"/> when the failure is the bundle as a whole.</summary>
    public string? RelativePath { get; }
}
