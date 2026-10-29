using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Autonogrammer.UI.Components.Shell;

/// <summary>
/// Holds the text a screen reader hears after the user navigates to another page of the app. A navigation arms it; the title of the page that
/// renders next is the announcement. Nothing is announced on the first load, because the browser already announces a loaded page. The app arms the
/// announcer itself before it changes the view and navigates (<see cref="Arm"/>), because the browser reports the new address after the render that shows
/// the new title; Back, Forward and links arm it through the address change.
/// </summary>
public sealed class RouteAnnouncer : IDisposable
{
    private readonly NavigationManager _navigation;
    private string? _expectedLocation;
    private bool _armed;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="RouteAnnouncer"/> class and starts listening for navigations.</summary>
    /// <param name="navigation">The navigation manager whose location changes arm the announcer.</param>
    public RouteAnnouncer(NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        _navigation = navigation;
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>Occurs after <see cref="Text"/> changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the current announcement; empty until the first navigation after the first load.</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// Arms the announcer for a navigation the app is about to make itself, so the title of the view it changes to is announced whether the navigation manager
    /// reports the new address before or after that view renders. The address change of that navigation does not arm the announcer again.
    /// </summary>
    /// <param name="destination">The address the app navigates to, absolute or relative to the base address.</param>
    public void Arm(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (_disposed)
        {
            return;
        }

        _armed = true;
        _expectedLocation = _navigation.ToAbsoluteUri(destination).AbsoluteUri;
    }

    /// <summary>Reports the title of a page that rendered. It becomes the announcement only when a navigation armed the announcer since the last announcement.</summary>
    /// <param name="title">The document title of the page.</param>
    public void Report(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (_disposed || !_armed)
        {
            return;
        }

        _armed = false;
        Text = title;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _navigation.LocationChanged -= OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_expectedLocation is not null && string.Equals(_expectedLocation, e.Location, StringComparison.Ordinal))
        {
            _expectedLocation = null;
            return;
        }

        _armed = true;
    }
}
