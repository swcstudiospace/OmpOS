using Autonogrammer.Presentation.Settings.Connectors;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Connectors page: one card per server, a connection chip, a Check connection button and a three-state control per tool.
/// The button is the only check. Wiring into the settings window belongs to a later plan.
/// </summary>
public partial class ConnectorsPageView : UserControl
{
    private ConnectorMatrixModel? _matrix;
    private IConnectorCheckSource? _checks;

    /// <summary>Creates a view with no cards.</summary>
    public ConnectorsPageView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the notice that Auto does not trust readOnlyHint.</summary>
    internal TextBlock NoticeText => Notice;

    /// <summary>Binds cards. A check runs only when the user presses Check connection.</summary>
    /// <param name="matrix">The permission matrix.</param>
    /// <param name="cards">The cards to show.</param>
    /// <param name="checks">The explicit check, or null when the button should do nothing.</param>
    public void Show(ConnectorMatrixModel matrix, IReadOnlyList<ConnectorCardModel> cards, IConnectorCheckSource? checks)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(cards);
        _matrix = matrix;
        _checks = checks;
        Cards.ItemsSource = cards;
    }

#pragma warning disable VSTHRD100 // The click handler is the button boundary. The check is awaited and a failure stays on the card.
    private async void OnCheckClick(object? sender, RoutedEventArgs e)
    {
        if (_checks is null || _matrix is null || sender is not Button { Tag: string serverId })
        {
            return;
        }

        var snapshot = await _checks.CheckAsync(serverId, CancellationToken.None).ConfigureAwait(true);
        if (Cards.ItemsSource is not IEnumerable<ConnectorCardModel> cards)
        {
            return;
        }

        var card = cards.FirstOrDefault(item => string.Equals(item.ServerId, serverId, StringComparison.Ordinal));
        card?.ApplyCheck(snapshot.State, _matrix.Rows(serverId, snapshot.ToolNames), _matrix.ServerState(serverId));
    }

#pragma warning restore VSTHRD100

    private void OnServerStateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_matrix is null || sender is not ComboBox { Tag: string serverId, SelectedItem: string label })
        {
            return;
        }

        _matrix.SetServerState(serverId, ConnectorStateLabels.Parse(label));
    }
}
