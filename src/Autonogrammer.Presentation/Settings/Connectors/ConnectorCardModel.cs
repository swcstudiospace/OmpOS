using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>One connector card: identity, connection state and tool rows. <see cref="ToString"/> prints names and states only.</summary>
public sealed class ConnectorCardModel : INotifyPropertyChanged
{
    private string _connectionState;
    private IReadOnlyList<ConnectorToolRow> _tools;

    /// <summary>Creates a card that has not been checked.</summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="displayName">The name shown on the card.</param>
    /// <param name="serverPermission">The server-level policy.</param>
    /// <param name="tools">The tool rows. Empty until a check lists tools.</param>
    /// <param name="connectionState">The connection chip. Null is "Not checked".</param>
    public ConnectorCardModel(
        string serverId,
        string displayName,
        ConnectorToolState serverPermission,
        IReadOnlyList<ConnectorToolRow>? tools = null,
        string? connectionState = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ServerId = serverId;
        DisplayName = displayName;
        ServerPermission = serverPermission;
        _tools = tools ?? [];
        _connectionState = connectionState ?? "Not checked";
    }

    /// <summary>Occurs when the connection chip or the tool rows change.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the server id.</summary>
    public string ServerId { get; }

    /// <summary>Gets the name shown on the card.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the server-level policy.</summary>
    public ConnectorToolState ServerPermission { get; private set; }

    /// <summary>Gets the connection chip text.</summary>
    public string ConnectionState => _connectionState;

    /// <summary>Gets the tool rows.</summary>
    public IReadOnlyList<ConnectorToolRow> Tools => _tools;

    /// <summary>Gets the accessible name of the card.</summary>
    public string CardName => "Connector " + DisplayName;

    /// <summary>Gets the accessible name of the connection chip.</summary>
    public string ChipName => "Connection state " + DisplayName;

    /// <summary>Gets the accessible name of the check button.</summary>
    public string CheckName => "Check connection " + ServerId;

    /// <summary>Gets the accessible name of the server policy control.</summary>
    public string ServerPermissionName => "Server " + ServerId + " permission";

    /// <summary>Gets the label of the server policy.</summary>
    public string ServerPermissionLabel => ConnectorStateLabels.Of(ServerPermission);

    /// <summary>Replaces the connection chip and the tool rows after an explicit check.</summary>
    /// <param name="connectionState">The state to show.</param>
    /// <param name="tools">The tool rows.</param>
    /// <param name="serverPermission">The server policy after the reload.</param>
    public void ApplyCheck(string connectionState, IReadOnlyList<ConnectorToolRow> tools, ConnectorToolState serverPermission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionState);
        ArgumentNullException.ThrowIfNull(tools);
        _connectionState = connectionState;
        _tools = tools;
        ServerPermission = serverPermission;
        Raise(nameof(ConnectionState));
        Raise(nameof(Tools));
        Raise(nameof(ServerPermission));
        Raise(nameof(ServerPermissionLabel));
    }

    /// <summary>Prints the id, the chip and the tool names. No argument, header, environment value or URL.</summary>
    /// <returns>A one-line description without a secret.</returns>
    public override string ToString() =>
        $"ConnectorCard {{ ServerId = {ServerId}, Connection = {ConnectionState}, Tools = {string.Join(',', Tools.Select(tool => tool.Name))} }}";

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
