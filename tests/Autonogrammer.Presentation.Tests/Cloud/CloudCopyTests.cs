using System.Reflection;
using Autonogrammer.Presentation.Cloud;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cloud;

/// <summary>The cloud copy: every text exists, reads as a finished sentence, and the formatted texts carry what they are given.</summary>
public sealed class CloudCopyTests
{
    private static IEnumerable<(string Name, string? Text)> Texts() =>
        typeof(CloudCopy).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, (string?)field.GetRawConstantValue()));

    /// <summary>Every text constant is present, trimmed, free of format placeholders and of leftover markers.</summary>
    [Fact]
    public void EveryTextIsPresentAndFinished()
    {
        var texts = Texts().ToArray();

        texts.Length.ShouldBeGreaterThan(100);
        foreach (var (name, text) in texts)
        {
            string.IsNullOrWhiteSpace(text).ShouldBeFalse($"{name} must not be empty");
            var value = text ?? string.Empty;
            value.ShouldBe(value.Trim(), $"{name} must not have surrounding spaces");
            value.Contains('{', StringComparison.Ordinal).ShouldBeFalse($"{name} must not hold a placeholder");
        }
    }

    /// <summary>The setup steps are four finished sentences that name what has to be connected, and none holds an address or a secret.</summary>
    [Fact]
    public void TheSetupStepsAreFinishedSentences()
    {
        CloudCopy.SetupSteps.Count.ShouldBe(4);
        CloudCopy.SetupSteps.ShouldAllBe(step => step.EndsWith('.') && step.Length > 20);
        string.Join(' ', CloudCopy.SetupSteps).ShouldContain("Railway", Case.Sensitive);
        string.Join(' ', CloudCopy.SetupSteps).ShouldContain("GitHub", Case.Sensitive);
        string.Join(' ', CloudCopy.SetupSteps).ShouldNotContain("http", Case.Sensitive);
        CloudCopy.SetupStepLine(2, CloudCopy.SetupSteps[1]).ShouldStartWith("2. ", Case.Sensitive);
    }

    /// <summary>The cost sentence names the billing and the idle time, and says minute for one.</summary>
    [Fact]
    public void TheCostNoticeNamesTheBillingAndTheIdleTime()
    {
        CloudCopy.CostNotice(15).ShouldBe("Railway bills compute while the worker is awake; idle workers sleep after 15 minutes.");
        CloudCopy.CostNotice(1).ShouldBe("Railway bills compute while the worker is awake; idle workers sleep after 1 minute.");
    }

    /// <summary>The usage line reads with and without caps, in the singular and the plural.</summary>
    [Fact]
    public void TheUsageLineReadsWithAndWithoutCaps()
    {
        CloudCopy.LimitsLine(new CloudLimits(1, null, 0, null)).ShouldBe("1 agent, 0 awake");
        CloudCopy.LimitsLine(new CloudLimits(3, 5, 1, 2)).ShouldBe("3 of 5 agents, 1 of 2 awake");
        CloudCopy.LimitsLine(new CloudLimits(1, 1, 1, null)).ShouldBe("1 of 1 agent, 1 awake");
    }

    /// <summary>The formatted texts carry the name or number they are given, so a screen reader hears what the control is about.</summary>
    [Fact]
    public void FormattedTextsCarryTheirArguments()
    {
        string[] named =
        [
            CloudCopy.RowAccessibleName("Fix", "Idle", "now"),
            CloudCopy.DeleteWarning("Fix"),
            CloudCopy.WakeName("Fix"),
            CloudCopy.StopName("Fix"),
            CloudCopy.DeleteName("Fix"),
            CloudCopy.ConfirmDeleteName("Fix"),
            CloudCopy.OpenInAgentsName("Fix"),
            CloudCopy.PromptName("Fix"),
            CloudCopy.SendName("Fix"),
            CloudCopy.WakeRequested("Fix"),
            CloudCopy.StopRequested("Fix"),
            CloudCopy.DeleteRequested("Fix"),
            CloudCopy.PromptSent("Fix"),
            CloudCopy.Created("Fix"),
            CloudCopy.HandoffStarted("Fix"),
        ];
        foreach (var text in named)
        {
            text.ShouldContain("Fix", Case.Sensitive);
        }

        string[] repository =
        [
            CloudCopy.PullRequestCreated("owner/app"),
            CloudCopy.PullRequestOpened("owner/app"),
            CloudCopy.OpenPullRequestName("owner/app"),
            CloudCopy.CreatePullRequestName("owner/app"),
            CloudCopy.RemoveRepositoryName("owner/app"),
        ];
        foreach (var text in repository)
        {
            text.ShouldContain("owner/app", Case.Sensitive);
        }

        CloudCopy.RepositoriesOnBranch("owner/app", "main").ShouldBe("owner/app on main");
        CloudCopy.RemoveSecretName("API_KEY").ShouldContain("API_KEY", Case.Sensitive);
        CloudCopy.AgentCapReached(7).ShouldContain("7", Case.Sensitive);
        CloudCopy.IdleInvalid(1, 240).ShouldContain("240", Case.Sensitive);
        CloudCopy.NoMatches("Failed").ShouldContain("Failed", Case.Sensitive);
    }

    /// <summary>The two reasons the entry copy gives for a secret name never ask for a value, and the secrets help says values never pass through the form.</summary>
    [Fact]
    public void TheSecretCopyTellsTheUserToEnterNamesOnly()
    {
        CloudCopy.SecretNameInvalid.ShouldContain("never the secret's value", Case.Sensitive);
        CloudCopy.SecretsHelp.ShouldContain("never typed or shown here", Case.Sensitive);
        CloudCopy.RepositoryHasCredentials.ShouldContain("Credentials never belong", Case.Sensitive);
    }
}
