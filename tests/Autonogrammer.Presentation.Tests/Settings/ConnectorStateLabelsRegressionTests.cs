using Autonogrammer.Presentation.Settings.Connectors;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The card combo box feeds its selected label back through Parse, so every label All lists must parse back to its state.</summary>
public sealed class ConnectorStateLabelsRegressionTests
{
    /// <summary>Each state's label parses back to that state, so a selection never reverts the saved policy.</summary>
    [Fact]
    public void EveryStateLabelParsesBackToItsState()
    {
        foreach (var state in Enum.GetValues<ConnectorToolState>())
        {
            ConnectorStateLabels.Parse(ConnectorStateLabels.Of(state)).ShouldBe(state);
        }
    }

    /// <summary>Parse accepts the labels that All lists, in enum order.</summary>
    [Fact]
    public void ParseAcceptsTheLabelsAllLists()
    {
        ConnectorStateLabels.Parse(ConnectorStateLabels.All[0]).ShouldBe(ConnectorToolState.AlwaysAllow);
        ConnectorStateLabels.Parse(ConnectorStateLabels.All[1]).ShouldBe(ConnectorToolState.NeedsApproval);
        ConnectorStateLabels.Parse(ConnectorStateLabels.All[2]).ShouldBe(ConnectorToolState.Blocked);
    }

    /// <summary>A missing or unlisted label falls back to NeedsApproval, the state with no rule.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Always allow tool")]
    public void UnlistedLabelFallsBackToNeedsApproval(string? label)
    {
        ConnectorStateLabels.Parse(label).ShouldBe(ConnectorToolState.NeedsApproval);
    }
}
