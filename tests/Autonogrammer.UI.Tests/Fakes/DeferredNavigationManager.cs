using Microsoft.AspNetCore.Components;

namespace Autonogrammer.UI.Tests.Fakes;

/// <summary>
/// A navigation manager that reports an address change after the caller's render, as the browser does in WebAssembly: a navigation only records its target,
/// and <see cref="DeliverAddressChanges"/> reports every recorded target the way the browser's callback does.
/// </summary>
internal sealed class DeferredNavigationManager : NavigationManager
{
    private readonly List<string> _pending = [];

    /// <summary>Initializes a new instance of the <see cref="DeferredNavigationManager"/> class at the root of <c>http://localhost/</c>.</summary>
    public DeferredNavigationManager() => Initialize("http://localhost/", "http://localhost/");

    /// <summary>Reports every navigation recorded so far, oldest first, and forgets them.</summary>
    public void DeliverAddressChanges()
    {
        var targets = _pending.ToArray();
        _pending.Clear();
        foreach (var target in targets)
        {
            Uri = target;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    /// <inheritdoc />
    protected override void NavigateToCore(string uri, NavigationOptions options) => _pending.Add(ToAbsoluteUri(uri).AbsoluteUri);
}
