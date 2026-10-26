using Avalonia.Controls;

namespace OmpOS.Native.Views.Mcp;

/// <summary>
/// The MCP tools panel: every registered tool grouped by server, each with an enable toggle for the model's session, and the
/// list of tools offered to the model for that session. Toggles are changed on the UI thread, so the offered list is redrawn
/// in place and the grouped tree is not rebuilt; a rebuilt toggle would lose keyboard focus.
/// </summary>
internal partial class McpToolsView : UserControl
{
    private McpToolsModel? _model;

    /// <summary>Creates the panel with no data context.</summary>
    public McpToolsView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the panel to the model in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.OfferedChanged -= OnOfferedChanged;
        }

        _model = DataContext as McpToolsModel;
        if (_model is not null)
        {
            _model.OfferedChanged += OnOfferedChanged;
        }

        Render();
    }

    private void OnOfferedChanged(object? sender, EventArgs e) => RenderOffered();

    private void Render()
    {
        SessionText.Text = _model is null ? string.Empty : "Toggles apply to session " + _model.SessionId + " only.";

        IReadOnlyList<McpServerGroup> servers = _model?.Servers ?? [];
        ServerGroups.ItemsSource = servers;
        NoToolsText.IsVisible = servers.Count == 0;

        RenderOffered();
    }

    private void RenderOffered()
    {
        IReadOnlyList<McpOfferedRow> offered = _model?.Offered ?? [];
        OfferedRows.ItemsSource = offered;
        NoOfferedText.IsVisible = offered.Count == 0;
    }
}
