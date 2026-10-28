namespace Autonogrammer.Api.Services.Providers;

/// <summary>The result of one write to omp. The message never holds a key or a path.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Message">The reason for a result other than <see cref="ProviderWriteStatus.Done"/>, or null.</param>
internal sealed record ProviderWriteResult(ProviderWriteStatus Status, string? Message = null)
{
    /// <summary>Gets a value indicating whether omp now uses the change.</summary>
    public bool IsDone => Status == ProviderWriteStatus.Done;

    /// <summary>Gets a successful result.</summary>
    public static ProviderWriteResult Success { get; } = new(ProviderWriteStatus.Done);
}
