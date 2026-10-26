namespace OmpOS.Harness.OmpConfig;

/// <summary>The result of setting the default model.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">A plain sentence for the user.</param>
public sealed record OmpDefaultModelResult(OmpDefaultModelStatus Status, string Message)
{
    /// <summary>Gets a value indicating whether the default model was set.</summary>
    public bool IsSet => Status == OmpDefaultModelStatus.Set;
}
