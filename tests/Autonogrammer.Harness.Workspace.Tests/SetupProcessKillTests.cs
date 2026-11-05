using System.ComponentModel;
using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// Verifies how a setup step reports a process tree that could not be stopped after cancellation: a refused kill is a
/// failed step with its reason, and a process that has already exited is not reported at all.
/// </summary>
public sealed class SetupProcessKillTests
{
    private const string SleepPath = "/bin/sleep";
    private const string TruePath = "/bin/true";

    [Fact]
    public async Task RefusedKillAfterCancellationIsReportedAsFailedStep()
    {
        HostCommand.Require(SleepPath);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var refusal = new Win32Exception(1, "Operation not permitted");

        var result = await SetupProcess.RunAsync(
            SleepPath,
            ["1"],
            Path.GetTempPath(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            _ => throw refusal,
            cancelled.Token);

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldBe($"could not stop '{SleepPath}' after cancellation: Operation not permitted");
    }

    [Fact]
    public void KillOfAlreadyExitedProcessIsNotReported()
    {
        HostCommand.Require(TruePath);
        using var process = Start(TruePath);
        process.WaitForExit();
        var killInvoked = false;

        var refusal = SetupProcess.KillProcessTree(process, _ =>
        {
            killInvoked = true;
            throw new Win32Exception(1, "Operation not permitted");
        });

        refusal.ShouldBeNull();
        killInvoked.ShouldBeFalse();
    }

    [Fact]
    public void ProcessThatExitsBeforeTheKillIsNotReported()
    {
        HostCommand.Require(SleepPath);
        using var process = Start(SleepPath, "1");

        var refusal = SetupProcess.KillProcessTree(process, _ => throw new InvalidOperationException("No process is associated."));

        refusal.ShouldBeNull();
        process.WaitForExit();
    }

    private static Process Start(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
    }
}
