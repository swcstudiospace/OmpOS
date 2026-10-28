namespace Autonogrammer.Sandbox;

/// <summary>
/// A cloud environment operation failed. Messages never contain credentials or variable values.
/// </summary>
public sealed class CloudEnvironmentException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CloudEnvironmentException"/> class.
    /// </summary>
    /// <param name="message">The error message; it must already be free of secrets.</param>
    /// <param name="failure">The failure category.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public CloudEnvironmentException(string message, CloudEnvironmentFailure failure, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudEnvironmentException"/> class with a default message.
    /// </summary>
    public CloudEnvironmentException()
        : base()
    {
        Failure = CloudEnvironmentFailure.Transport;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudEnvironmentException"/> class with an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CloudEnvironmentException(string message)
        : base(message)
    {
        Failure = CloudEnvironmentFailure.Transport;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudEnvironmentException"/> class with an error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public CloudEnvironmentException(string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = CloudEnvironmentFailure.Transport;
    }

    /// <summary>Gets the failure category.</summary>
    public CloudEnvironmentFailure Failure { get; }

    /// <summary>Gets how long the provider asked the caller to wait before retrying, when known.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Gets the provider trace identifier of the failed request, when the provider returned one.</summary>
    public string? TraceId { get; init; }

    /// <summary>Gets the provider operation that failed, for example <c>serviceCreate</c>.</summary>
    public string? Operation { get; init; }
}
