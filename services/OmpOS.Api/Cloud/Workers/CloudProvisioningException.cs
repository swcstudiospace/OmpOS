namespace OmpOS.Api.Cloud;

/// <summary>Provisioning or waking a worker failed for a reason OmpOS can name. The message is written by OmpOS and never repeats a secret value.</summary>
internal sealed class CloudProvisioningException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CloudProvisioningException"/> class with the generic code <c>provisioning_failed</c>.</summary>
    public CloudProvisioningException()
        : this("provisioning_failed", "Provisioning the worker failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CloudProvisioningException"/> class with the generic code <c>provisioning_failed</c>.</summary>
    /// <param name="message">A safe explanation.</param>
    public CloudProvisioningException(string message)
        : this("provisioning_failed", message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CloudProvisioningException"/> class with the generic code <c>provisioning_failed</c>.</summary>
    /// <param name="message">A safe explanation.</param>
    /// <param name="innerException">The underlying failure.</param>
    public CloudProvisioningException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = "provisioning_failed";
    }

    /// <summary>Initializes a new instance of the <see cref="CloudProvisioningException"/> class.</summary>
    /// <param name="code">The stable machine-readable code stored on the agent.</param>
    /// <param name="message">A safe explanation.</param>
    public CloudProvisioningException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Gets the stable machine-readable code.</summary>
    public string Code { get; }
}
