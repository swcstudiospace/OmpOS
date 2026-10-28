namespace OmpOS.Api.Cloud;

/// <summary>Remembers that the cloud routes were mapped on this host, so mapping them from two places (the host's own composition and a test) cannot register a route twice and make it ambiguous.</summary>
internal sealed class CloudEndpointsMapped
{
    private int _mapped;

    /// <summary>Marks the routes as mapped.</summary>
    /// <returns>True for the first call, false for every later one.</returns>
    public bool TryMark() => Interlocked.Exchange(ref _mapped, 1) == 0;
}
