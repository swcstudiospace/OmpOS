using System.Security.Cryptography;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// Verifies that repository setup steps run only after the user approved them for that repository, that an approval is
/// keyed by repository path and setup file hash, and that a changed file asks again (finding M-16).
/// </summary>
public sealed class SetupRunnerConsentTests
{
    private const string ShellPath = "/bin/sh";
    private const string SetupFile = ".autonogrammer/setup.json";
    private const string RanMarker = "ran.txt";

    [Fact]
    public async Task SetupWithoutConsentIsRefusedAndNeitherStepsNorAgentRun()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                new MemorySetupConsentStore(),
                new DenyingSetupConsentPrompt(),
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.ConsentRefused);
        result.FailedStepIndex.ShouldBeNull();
        result.Environment.ShouldBeEmpty();
        result.FailureReason.ShouldNotBeNull().ShouldContain(SetupFile);
        result.FailureReason.ShouldContain("cannot ask for one");
        starts.ShouldBe(0);
        File.Exists(Path.Combine(workspace.RootPath, RanMarker)).ShouldBeFalse();
    }

    [Fact]
    public async Task RefusalByTheUserRunsNothingAndIsAskedAgainNextTime()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));
        var store = new MemorySetupConsentStore();
        var prompt = ScriptedSetupConsentPrompt.Refusing();

        var first = await RunAsync(workspace, store, prompt).ConfigureAwait(true);
        var second = await RunAsync(workspace, store, prompt).ConfigureAwait(true);

        first.Result.Status.ShouldBe(SetupStatus.ConsentRefused);
        first.Result.FailureReason.ShouldNotBeNull().ShouldContain("the user pressed Deny");
        second.Result.Status.ShouldBe(SetupStatus.ConsentRefused);
        prompt.Requests.Count.ShouldBe(2);
        store.RecordCount.ShouldBe(0);
        File.Exists(Path.Combine(workspace.RootPath, RanMarker)).ShouldBeFalse();
    }

    [Fact]
    public async Task SetupRunsAfterConsentAndIsNotAskedAgainWhileTheFileIsUnchanged()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));
        using var store = new FileSetupConsentStore(Path.Combine(workspace.RootPath, "state", "consent.json"));
        var prompt = ScriptedSetupConsentPrompt.Approving();

        var first = await RunAsync(workspace, store, prompt).ConfigureAwait(true);
        File.Delete(Path.Combine(workspace.RootPath, RanMarker));
        var second = await RunAsync(workspace, store, prompt).ConfigureAwait(true);

        first.Result.Status.ShouldBe(SetupStatus.Passed);
        first.Starts.ShouldBe(1);
        second.Result.Status.ShouldBe(SetupStatus.Passed);
        second.Starts.ShouldBe(1);
        prompt.Requests.Count.ShouldBe(1);
        (await File.ReadAllTextAsync(Path.Combine(workspace.RootPath, RanMarker), TestContext.Current.CancellationToken).ConfigureAwait(true)).Trim().ShouldBe("first");
    }

    [Fact]
    public async Task ChangedSetupFileAsksAgainAndANewRefusalStopsTheChangedSteps()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));
        var store = new MemorySetupConsentStore();
        var approving = ScriptedSetupConsentPrompt.Approving();
        var refusing = ScriptedSetupConsentPrompt.Refusing();

        var original = await RunAsync(workspace, store, approving).ConfigureAwait(true);
        File.Delete(Path.Combine(workspace.RootPath, RanMarker));
        workspace.Write(SetupFile, MarkerSetup("second"));
        var changed = await RunAsync(workspace, store, refusing).ConfigureAwait(true);

        original.Result.Status.ShouldBe(SetupStatus.Passed);
        changed.Result.Status.ShouldBe(SetupStatus.ConsentRefused);
        changed.Starts.ShouldBe(0);
        refusing.Requests.Count.ShouldBe(1);
        refusing.Requests[0].Key.SetupSha256.ShouldNotBe(approving.Requests[0].Key.SetupSha256, StringComparer.Ordinal);
        File.Exists(Path.Combine(workspace.RootPath, RanMarker)).ShouldBeFalse();
    }

    [Fact]
    public async Task ApprovalOfOneRepositoryDoesNotCoverAnotherWithIdenticalSetup()
    {
        HostCommand.Require(ShellPath);
        using var first = new TempWorkspace();
        first.Write(SetupFile, MarkerSetup("same"));
        using var second = new TempWorkspace();
        second.Write(SetupFile, MarkerSetup("same"));
        var store = new MemorySetupConsentStore();
        var prompt = ScriptedSetupConsentPrompt.Approving();

        await RunAsync(first, store, prompt).ConfigureAwait(true);
        await RunAsync(second, store, prompt).ConfigureAwait(true);

        prompt.Requests.Count.ShouldBe(2);
        prompt.Requests[0].Key.SetupSha256.ShouldBe(prompt.Requests[1].Key.SetupSha256);
        prompt.Requests[0].Key.RepositoryPath.ShouldBe(first.Root.FullPath);
        prompt.Requests[1].Key.RepositoryPath.ShouldBe(second.Root.FullPath);
    }

    [Fact]
    public async Task PromptReceivesTheCommandsAndTheHashOfTheExactFileBytes()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));
        var prompt = ScriptedSetupConsentPrompt.Refusing();

        await RunAsync(workspace, new MemorySetupConsentStore(), prompt).ConfigureAwait(true);

        var request = prompt.Requests.ShouldHaveSingleItem();
        request.SetupFile.ShouldBe(SetupFile);
        request.Key.SetupSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(workspace.RootPath, SetupFile), TestContext.Current.CancellationToken).ConfigureAwait(true))));
        request.Setup.Steps.ShouldHaveSingleItem().Command.ShouldBe(ShellPath);
    }

    [Fact]
    public async Task SetupThatDeclaresOnlyEnvironmentVariablesNeedsConsentToo()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, """{ "steps": [], "environment": { "LD_PRELOAD": "/tmp/evil.so" } }""");
        var prompt = ScriptedSetupConsentPrompt.Refusing();

        var outcome = await RunAsync(workspace, new MemorySetupConsentStore(), prompt).ConfigureAwait(true);

        outcome.Result.Status.ShouldBe(SetupStatus.ConsentRefused);
        outcome.Starts.ShouldBe(0);
        prompt.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SetupThatDeclaresNothingToRunAsksNothing()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, """{ "steps": [] }""");
        var prompt = ScriptedSetupConsentPrompt.Refusing();

        var outcome = await RunAsync(workspace, new MemorySetupConsentStore(), prompt).ConfigureAwait(true);

        outcome.Result.Status.ShouldBe(SetupStatus.Passed);
        outcome.Starts.ShouldBe(1);
        prompt.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ApprovalThatCannotBeSavedFailsTheRunBeforeAnyStepRuns()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));

        var outcome = await RunAsync(workspace, new UnwritableSetupConsentStore(), ScriptedSetupConsentPrompt.Approving())
            .ConfigureAwait(true);

        outcome.Result.Status.ShouldBe(SetupStatus.Failed);
        outcome.Result.FailureReason.ShouldNotBeNull().ShouldContain("could not be saved");
        outcome.Starts.ShouldBe(0);
        File.Exists(Path.Combine(workspace.RootPath, RanMarker)).ShouldBeFalse();
    }

    [Fact]
    public async Task RunWithCorruptConsentFileAsksAndThenRuns()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, MarkerSetup("first"));
        var consentPath = Path.Combine(workspace.RootPath, "state", "consent.json");
        Directory.CreateDirectory(Path.GetDirectoryName(consentPath)!);
        await File.WriteAllTextAsync(consentPath, "{ this is not json", TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var store = new FileSetupConsentStore(consentPath);
        var prompt = ScriptedSetupConsentPrompt.Approving();

        var outcome = await RunAsync(workspace, store, prompt).ConfigureAwait(true);

        outcome.Result.Status.ShouldBe(SetupStatus.Passed);
        prompt.Requests.Count.ShouldBe(1);
    }

    private static string MarkerSetup(string marker) =>
        "{ \"steps\": [ { \"command\": \"/bin/sh\", \"arguments\": [\"-c\", \"echo " + marker + " > " + RanMarker + "\"] } ] }";

    private static async Task<(SetupRunResult Result, int Starts)> RunAsync(
        TempWorkspace workspace,
        ISetupConsentStore store,
        ISetupConsentPrompt prompt)
    {
        var starts = 0;
        var result = await SetupRunner.RunAsync(
                workspace.Root,
                store,
                prompt,
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);
        return (result, starts);
    }
}
