using System.ComponentModel;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Terminal.Tests;

/// <summary>
/// Verifies the production runner against a real process: exit code, captured output, output bound and start failure.
/// The probe is the <c>dotnet</c> CLI that runs these tests.
/// </summary>
public sealed class SystemProcessRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReportsExitCodeAndCapturedStandardOutput()
    {
        using var workspace = new TempWorkspace();
        var runner = new SystemProcessRunner();

        var outcome = await runner.RunAsync(
            new ProcessRequest("dotnet", ["--version"], workspace.Path, 4096),
            Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.Trim().ShouldNotBeEmpty();
        outcome.StandardOutputTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task ReportsNonZeroExitCodeAndStandardError()
    {
        using var workspace = new TempWorkspace();
        var runner = new SystemProcessRunner();

        var outcome = await runner.RunAsync(
            new ProcessRequest("dotnet", ["ompos-no-such-command"], workspace.Path, 4096),
            Token);

        outcome.ExitCode.ShouldNotBe(0);
        outcome.StandardError.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task KeepsOnlyTheConfiguredNumberOfOutputCharacters()
    {
        using var workspace = new TempWorkspace();
        var runner = new SystemProcessRunner();

        var outcome = await runner.RunAsync(
            new ProcessRequest("dotnet", ["--help"], workspace.Path, 20),
            Token);

        outcome.StandardOutput.Length.ShouldBe(20);
        outcome.StandardOutputTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingWorkingDirectoryFailsToStart()
    {
        using var workspace = new TempWorkspace();
        var runner = new SystemProcessRunner();
        var missing = Path.Combine(workspace.Path, "does-not-exist");

        await Should.ThrowAsync<Win32Exception>(() =>
            runner.RunAsync(new ProcessRequest("dotnet", ["--version"], missing, 4096), Token));
    }
}
