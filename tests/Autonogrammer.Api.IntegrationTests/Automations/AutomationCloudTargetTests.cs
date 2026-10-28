using Autonogrammer.Api.Automations;
using Autonogrammer.Harness.Automations.Hosted;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Automations;

/// <summary>Verifies how the cloud target turns an automation into a cloud agent request and how launcher results become run results.</summary>
public sealed class AutomationCloudTargetTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AutomationRunRequest Request(UserAutomation automation) =>
        new(automation, "0123456789abcdef", "the prompt", AutomationTriggerContext.Manual());

    private static UserAutomation Automation(AutomationTargetSpec? target = null, string name = "Nightly review") =>
        new()
        {
            Id = "aut_1",
            Owner = "alice",
            Name = name,
            Prompt = "p",
            Target = target ?? new AutomationTargetSpec { Repositories = [new AutomationRepository("https://github.com/acme/widgets", "main", "develop")] },
        };

    [Fact]
    public async Task The_agent_request_carries_the_repositories_secret_names_environment_and_hints_and_the_run_id_is_the_key()
    {
        var launcher = new FakeCloudAgentLauncher();
        var target = new CloudAutomationTarget(launcher);
        var spec = new AutomationTargetSpec
        {
            Repositories = [new AutomationRepository("https://github.com/acme/widgets", "main", "develop"), new AutomationRepository("https://github.com/acme/gadgets")],
            SecretRefs = ["NPM_TOKEN"],
            EnvironmentId = "node-22",
            Model = "claude-sonnet",
            Mode = "plan",
        };

        var result = await target.RunAsync(Request(Automation(spec)), Token).ConfigureAwait(true);

        result.Status.ShouldBe(AutomationRunStatus.Launched);
        result.ExternalId.ShouldBe("agent-01234567");
        var launch = launcher.Launches.ShouldHaveSingleItem();
        launch.Owner.ShouldBe("alice");
        launch.Key.ShouldBe("0123456789abcdef");
        launch.Prompt.ShouldBe("the prompt");
        launch.Request.Repositories!.Count.ShouldBe(2);
        launch.Request.Repositories[0].Address.ShouldBe("https://github.com/acme/widgets");
        launch.Request.Repositories[0].Branch.ShouldBe("main");
        launch.Request.Repositories[0].BaseBranch.ShouldBe("develop");
        launch.Request.Repositories[1].Branch.ShouldBeNull();
        launch.Request.SecretRefs.ShouldBe(["NPM_TOKEN"]);
        launch.Request.EnvironmentId.ShouldBe("node-22");
        launch.Request.Model.ShouldBe("claude-sonnet");
        launch.Request.Mode.ShouldBe("plan");
    }

    [Fact]
    public async Task No_secret_names_means_none_are_requested_and_a_long_name_is_cut_to_the_agent_name_limit()
    {
        var launcher = new FakeCloudAgentLauncher();
        var target = new CloudAutomationTarget(launcher);

        _ = await target.RunAsync(Request(Automation(name: new string('n', 100))), Token).ConfigureAwait(true);

        var launch = launcher.Launches.ShouldHaveSingleItem();
        launch.Request.SecretRefs.ShouldBeNull();
        launch.Request.Name.ShouldNotBeNull().Length.ShouldBe(100);
    }

    [Fact]
    public async Task A_failed_launch_is_a_failed_result_with_the_launchers_sentence()
    {
        var launcher = new FakeCloudAgentLauncher { FailWith = "The cloud agent was refused (agent_limit_reached)." };
        var target = new CloudAutomationTarget(launcher);

        var result = await target.RunAsync(Request(Automation()), Token).ConfigureAwait(true);

        result.Status.ShouldBe(AutomationRunStatus.Failed);
        result.Error.ShouldBe("The cloud agent was refused (agent_limit_reached).");
        result.ExternalId.ShouldBeNull();
    }

    [Fact]
    public void The_target_kind_is_cloud()
    {
        new CloudAutomationTarget(new FakeCloudAgentLauncher()).Kind.ShouldBe(AutomationTargetKind.Cloud);
    }

    [Fact]
    public async Task Without_cloud_agents_every_launch_fails_with_a_sentence_that_says_why()
    {
        var result = await new UnavailableCloudAgentLauncher().LaunchAsync("alice", new Autonogrammer.Contracts.Cloud.CreateCloudAgentRequest(), "p", "key", Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.AgentId.ShouldBeNull();
        result.Error.ShouldNotBeNull().ShouldContain("Cloud:Enabled", Case.Sensitive);
    }
}
