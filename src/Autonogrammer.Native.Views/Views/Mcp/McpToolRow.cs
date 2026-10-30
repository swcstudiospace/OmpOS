using Autonogrammer.Harness.Mcp;

namespace Autonogrammer.Native.Views.Mcp;

/// <summary>
/// One tool in the grouped list. <see cref="Enabled"/> is the toggle's two-way value: setting it enables or disables the tool
/// for the model's session.
/// </summary>
internal sealed class McpToolRow
{
    private readonly McpToolsModel _owner;
    private bool _enabled;

    /// <summary>Creates a row for one tool.</summary>
    /// <param name="owner">The model that applies the toggle.</param>
    /// <param name="id">The tool.</param>
    /// <param name="description">The description the server advertised, if any.</param>
    /// <param name="enabled">Whether the tool is enabled for the session now.</param>
    public McpToolRow(McpToolsModel owner, McpToolId id, string? description, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        Id = id;
        Description = description ?? string.Empty;
        _enabled = enabled;
    }

    /// <summary>Gets the tool.</summary>
    public McpToolId Id { get; }

    /// <summary>Gets the tool name the server advertised.</summary>
    public string ToolName => Id.ToolName;

    /// <summary>Gets the server and tool name, used as the accessible name of the toggle.</summary>
    public string Name => Id.ToString();

    /// <summary>Gets the description the server advertised, or an empty string.</summary>
    public string Description { get; }

    /// <summary>Gets or sets whether the tool is enabled for the session. Changing it changes the session.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value == _enabled)
            {
                return;
            }

            _enabled = value;
            _owner.SetEnabled(Id, value);
        }
    }
}
