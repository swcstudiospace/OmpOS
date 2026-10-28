namespace Autonogrammer.Api.Automations;

/// <summary>Remembers that the automation routes were mapped on this host, so mapping them from two places cannot register a route twice and make it ambiguous.</summary>
internal sealed class AutomationEndpointsMapped
{
    private int _mapped;

    /// <summary>Marks the routes as mapped.</summary>
    /// <returns>True for the first call, false for every later one.</returns>
    public bool TryMark() => Interlocked.Exchange(ref _mapped, 1) == 0;
}
