namespace OmpOS.Web.Services;

/// <summary>The default <see cref="IApiTokenProvider"/>: the browser holds no credential.</summary>
internal sealed class NoApiTokenProvider : IApiTokenProvider
{
    /// <inheritdoc />
    public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);
}
