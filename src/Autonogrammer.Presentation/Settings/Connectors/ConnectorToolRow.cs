namespace Autonogrammer.Presentation.Settings.Connectors;

using System.ComponentModel;

/// <summary>One tool row on a connector card.</summary>
public sealed class ConnectorToolRow : INotifyPropertyChanged
{
    private ConnectorToolState _state;

    /// <summary>Creates a row.</summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="name">The tool name.</param>
    /// <param name="state">The policy.</param>
    public ConnectorToolRow(string serverId, string name, ConnectorToolState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ServerId = serverId;
        Name = name;
        _state = state;
    }

    /// <summary>Occurs when the selected label changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the server id.</summary>
    public string ServerId { get; }

    /// <summary>Gets the tool name.</summary>
    public string Name { get; }

    /// <summary>Gets the policy.</summary>
    public ConnectorToolState State => _state;

    /// <summary>Gets the accessible name of the three-state control.</summary>
    public string AccessibleName => "Tool " + Name + " on " + ServerId;

    /// <summary>Gets or sets the label bound to the three-state control.</summary>
    public string SelectedLabel
    {
        get => ConnectorStateLabels.Of(_state);
        set
        {
            var next = ConnectorStateLabels.Parse(value);
            if (next == _state)
            {
                return;
            }

            _state = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        }
    }

    /// <summary>Prints the tool name and policy.</summary>
    /// <returns>A one-line description without a secret.</returns>
    public override string ToString() => Name + ":" + _state;
}
