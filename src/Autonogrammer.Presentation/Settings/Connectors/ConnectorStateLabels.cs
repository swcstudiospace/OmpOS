namespace Autonogrammer.Presentation.Settings.Connectors;

/// <summary>The words of <see cref="ConnectorToolState"/> on a card.</summary>
public static class ConnectorStateLabels
{
    /// <summary>Gets the three labels, in enum order.</summary>
    public static IReadOnlyList<string> All { get; } = ["Always allow", "Needs approval", "Blocked"];

    /// <summary>Returns the label of a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Of(ConnectorToolState state) => state switch
    {
        ConnectorToolState.AlwaysAllow => All[0],
        ConnectorToolState.Blocked => All[2],
        _ => All[1],
    };

    /// <summary>Returns the state of a label.</summary>
    /// <param name="label">The label.</param>
    /// <returns>The state. An unknown label is <see cref="ConnectorToolState.NeedsApproval"/>.</returns>
    public static ConnectorToolState Parse(string? label) => label switch
    {
        "Always allow" => ConnectorToolState.AlwaysAllow,
        "Blocked" => ConnectorToolState.Blocked,
        _ => ConnectorToolState.NeedsApproval,
    };
}
