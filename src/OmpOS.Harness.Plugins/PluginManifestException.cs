namespace OmpOS.Harness.Plugins;

/// <summary>
/// Thrown when a manifest fails validation. <see cref="Kind"/> names the rule that failed.
/// </summary>
public sealed class PluginManifestException : Exception
{
    /// <summary>
    /// Initializes a new instance with the generic invalid-manifest kind.
    /// </summary>
    public PluginManifestException()
        : this(PluginRejectionKind.InvalidManifest, null, "The manifest is invalid.")
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and the generic invalid-manifest kind.
    /// </summary>
    /// <param name="message">What is wrong with the manifest.</param>
    public PluginManifestException(string message)
        : this(PluginRejectionKind.InvalidManifest, null, message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and an inner exception, and the generic invalid-manifest kind.
    /// </summary>
    /// <param name="message">What is wrong with the manifest.</param>
    /// <param name="innerException">The exception that caused the rejection.</param>
    public PluginManifestException(string message, Exception innerException)
        : this(PluginRejectionKind.InvalidManifest, null, message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="kind">The rule that failed.</param>
    /// <param name="pluginId">The plugin identifier, when the manifest supplied a valid one; otherwise null.</param>
    /// <param name="message">A message that says what is wrong with the manifest.</param>
    public PluginManifestException(PluginRejectionKind kind, string? pluginId, string message)
        : base(message)
    {
        Kind = kind;
        PluginId = pluginId;
    }

    /// <summary>
    /// Initializes a new instance with an inner exception, such as the JSON parser's error.
    /// </summary>
    /// <param name="kind">The rule that failed.</param>
    /// <param name="pluginId">The plugin identifier, when known; otherwise null.</param>
    /// <param name="message">A message that says what is wrong with the manifest.</param>
    /// <param name="innerException">The exception that caused the rejection.</param>
    public PluginManifestException(PluginRejectionKind kind, string? pluginId, string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
        PluginId = pluginId;
    }

    /// <summary>
    /// The rule that failed.
    /// </summary>
    public PluginRejectionKind Kind { get; }

    /// <summary>
    /// The plugin identifier, when the manifest supplied a valid one; otherwise null.
    /// </summary>
    public string? PluginId { get; }
}
