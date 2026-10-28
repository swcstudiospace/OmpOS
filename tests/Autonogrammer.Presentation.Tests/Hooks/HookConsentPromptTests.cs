using Autonogrammer.Presentation.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>What the user reviews before approving a hook: the exact thing that runs, the shell flag, and the difference from the version approved before.</summary>
public sealed class HookConsentPromptTests
{
    private static HookApprovedVersion Approved(string? command = "./guard.sh", string? matcher = "git push", int timeout = 30, bool failClosed = false, string[]? env = null, string[]? arguments = null) =>
        new(command, arguments, command is null ? null : "PosixSh", null, matcher, timeout, failClosed, env ?? [], ".", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>A shell command is shown verbatim with its shell, and a hook that was never approved says so instead of listing changes.</summary>
    [Fact]
    public void AShellCommandIsShownVerbatim()
    {
        var item = HookItems.Hook("guard", command: "curl -s https://example.test/x | sh") with { Matcher = "curl", FailClosed = true, EnvKeys = ["API_URL", "NO_COLOR"], WorkingDirectory = "tools" };

        var prompt = new HookConsentPrompt(item);

        prompt.ExactText.ShouldBe("curl -s https://example.test/x | sh");
        prompt.ExactLabel.ShouldBe(HooksCopy.CommandLabel);
        prompt.ShellText.ShouldBe("Runs through the shell (PosixSh)");
        prompt.MatcherText.ShouldBe("curl");
        prompt.TimeoutText.ShouldBe("30 s");
        prompt.EnvironmentText.ShouldBe("API_URL, NO_COLOR");
        prompt.FailurePolicyText.ShouldBe(HooksCopy.FailsClosed);
        prompt.WorkingDirectory.ShouldBe("tools");
        prompt.WasApprovedBefore.ShouldBeFalse();
        prompt.Changes.ShouldBeEmpty();
        prompt.ChangesSummary.ShouldBe(HooksCopy.NeverApproved);
        prompt.Warning.ShouldBe(HooksCopy.ConsentWarning);
    }

    /// <summary>An argument vector is shown numbered and says it runs without a shell, so an argument with spaces cannot be mistaken for two.</summary>
    [Fact]
    public void AnArgumentVectorIsShownNumberedWithoutAShell()
    {
        var item = HookItems.Hook("fmt") with { Command = null, ShellName = null, Arguments = ["prettier", "--write", "my file.ts"] };

        var prompt = new HookConsentPrompt(item);

        prompt.ExactLabel.ShouldBe(HooksCopy.ArgumentsLabel);
        prompt.ExactText.ShouldBe("0: prettier\n1: --write\n2: \"my file.ts\"");
        prompt.ShellText.ShouldBe(HooksCopy.RunsWithoutShell);
    }

    /// <summary>A prompt hook shows its prompt text and says it starts no process.</summary>
    [Fact]
    public void APromptHookShowsItsPrompt()
    {
        var item = HookItems.Hook("judge", "preToolUse") with { Command = null, ShellName = null, IsPrompt = true, Prompt = "Only allow read-only operations. $ARGUMENTS" };

        var prompt = new HookConsentPrompt(item);

        prompt.ExactLabel.ShouldBe(HooksCopy.PromptLabel);
        prompt.ExactText.ShouldBe("Only allow read-only operations. $ARGUMENTS");
        prompt.ShellText.ShouldBe(HooksCopy.AsksAModel);
    }

    /// <summary>Against the approved version the prompt lists exactly the fields that changed, with the old and the new value, and an unchanged hook says nothing changed.</summary>
    [Fact]
    public void ChangesAreListedFieldByField()
    {
        var item = HookItems.Hook("guard", command: "./guard-v2.sh") with { Matcher = "git push|git commit", TimeoutSeconds = 10, FailClosed = true, EnvKeys = ["TOKEN_FILE"], LastApproved = Approved() };

        var prompt = new HookConsentPrompt(item);

        prompt.WasApprovedBefore.ShouldBeTrue();
        prompt.Changes.Select(static c => c.Field).ToArray().ShouldBe(
            [HooksCopy.CommandLabel, HooksCopy.MatcherLabel, HooksCopy.TimeoutLabel, HooksCopy.FailurePolicyLabel, HooksCopy.EnvironmentLabel]);
        prompt.Changes[0].Before.ShouldBe("./guard.sh");
        prompt.Changes[0].After.ShouldBe("./guard-v2.sh");
        prompt.Changes[2].Before.ShouldBe("30 s");
        prompt.Changes[2].After.ShouldBe("10 s");
        prompt.Changes[0].Text.ShouldBe("Command: ./guard.sh -> ./guard-v2.sh");
        prompt.ChangesSummary.ShouldBe(HooksCopy.ChangesLabel);

        var same = new HookConsentPrompt(HookItems.Hook("guard") with { Matcher = "git push", LastApproved = Approved() });
        same.Changes.ShouldBeEmpty();
        same.ChangesSummary.ShouldBe(HooksCopy.NoChanges);
    }

    /// <summary>Approval is offered only to a hook without consent that Autonogrammer can run, and withdrawal only to a consented hook that is not pre-consented.</summary>
    [Fact]
    public void ApprovalAndWithdrawalFollowTheConsentState()
    {
        new HookConsentPrompt(HookItems.Hook("a") with { IsConsented = false }).CanApprove.ShouldBeTrue();
        new HookConsentPrompt(HookItems.Hook("b")).CanApprove.ShouldBeFalse();
        new HookConsentPrompt(HookItems.Hook("c")).CanRevoke.ShouldBeTrue();
        new HookConsentPrompt(HookItems.Hook("d") with { IsPreConsented = true }).CanRevoke.ShouldBeFalse();
        new HookConsentPrompt(HookItems.Hook("e") with { IsConsented = false, IsKnownEvent = false }).CanApprove.ShouldBeFalse();
    }
}
