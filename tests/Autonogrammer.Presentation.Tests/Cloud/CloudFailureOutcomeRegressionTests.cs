using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cloud;

/// <summary>The new-agent dialog and the hand-off map one failure to the same outcome, so a refusal reads the same wherever it is shown.</summary>
public sealed class CloudFailureOutcomeRegressionTests
{
    private readonly ManualTimeProvider _clock = new();

    /// <summary>For every failure kind the dialog and the hand-off end with the same outcome name and the same sentence.</summary>
    [Theory]
    [InlineData(CloudFailureKind.Unavailable)]
    [InlineData(CloudFailureKind.NotConfigured)]
    [InlineData(CloudFailureKind.Unauthorized)]
    [InlineData(CloudFailureKind.LimitReached)]
    [InlineData(CloudFailureKind.QuotaExceeded)]
    [InlineData(CloudFailureKind.NotFound)]
    [InlineData(CloudFailureKind.Conflict)]
    [InlineData(CloudFailureKind.Rejected)]
    public async Task TheDialogAndTheHandoffReportTheSameFailureTheSameWay(CloudFailureKind kind)
    {
        var dialogHost = CloudFixtures.Host(_clock);
        dialogHost.FailNext("Create", new CloudHostException(kind, "The service said no."));
        var form = CloudFixtures.Form(dialogHost);
        form.CostConfirmed = true;
        var dialog = await form.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var handoffHost = CloudFixtures.Host(_clock);
        handoffHost.FailNext("Create", new CloudHostException(kind, "The service said no."));
        var request = new CloudHandoffRequest
        {
            Prompt = "Fix the flaky test",
            Repositories = [CloudFixtures.Repo()],
            CostConfirmed = true,
        };
        var handoff = await new CloudHandoff(handoffHost).StartAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);

        dialog.Outcome.ToString().ShouldBe(handoff.Outcome.ToString());
        dialog.Message.ShouldBe(handoff.Message);
    }
}
