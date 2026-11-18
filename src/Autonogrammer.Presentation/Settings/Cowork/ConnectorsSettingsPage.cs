using System.Collections.ObjectModel;
using Autonogrammer.Presentation.Settings.Connectors;

namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>
/// The connectors page. Loading lists the servers and their cards; a check runs only when the user asks for it. Auto does not trust readOnlyHint.
/// </summary>
public sealed class ConnectorsSettingsPage : SettingsPageBase
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<ConnectorServerListing>>> _servers;
    private readonly ObservableCollection<ConnectorCardModel> _cards = [];

    /// <summary>Creates the page.</summary>
    /// <param name="store">The three-state matrix store.</param>
    /// <param name="servers">Reads the servers the page lists. A failure the user can read is a <see cref="SettingsSourceException"/>.</param>
    /// <param name="checks">Runs the explicit connection check of a card.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ConnectorsSettingsPage(
        IConnectorPermissionStore store,
        Func<CancellationToken, Task<IReadOnlyList<ConnectorServerListing>>> servers,
        IConnectorCheckSource checks)
        : base(SettingsSection.Connectors, SettingsCopy.ConnectorsTitle, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(checks);
        Model = new ConnectorMatrixModel(store);
        _servers = servers;
        Checks = checks;
    }

    /// <summary>Gets the matrix the view reads.</summary>
    public ConnectorMatrixModel Model { get; }

    /// <summary>Gets the check source the view runs when the user presses Check connection.</summary>
    public IConnectorCheckSource Checks { get; }

    /// <summary>
    /// Gets the cards, one per listed server. The same collection is kept for the life of the page, and a load replaces its contents,
    /// so a view bound to it shows the list the load made.
    /// </summary>
    public IReadOnlyList<ConnectorCardModel> Cards => _cards;

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) =>
        RunLoadAsync(async token =>
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            var servers = await _servers(token).ConfigureAwait(true);
            _cards.Clear();
            foreach (var card in Model.Cards(servers))
            {
                _cards.Add(card);
            }
        }, cancellationToken);
}
