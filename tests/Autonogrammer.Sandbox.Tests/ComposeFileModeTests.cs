using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Verifies that the compose file the podman provider writes into the shared temp directory is private to the current
/// user from the moment it exists: a 0700 directory holding a 0600 file (finding M-15).
/// </summary>
public sealed class ComposeFileModeTests
{
    private const string ContainerId = "abcdef123456";
    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerAll = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>The compose file is mode 0600 and its directory is mode 0700 on Unix; on Windows it is written and readable.</summary>
    [Fact]
    public async Task ComposeFileIsOwnerOnlyInAPrivateDirectory()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok(ContainerId));
        var provider = PodmanTestData.CreateProvider(runner, options: new SandboxProviderOptions { PerSandboxCredentials = false });
        var ct = TestContext.Current.CancellationToken;

        var result = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);

        var composeFile = ComposeFileOf(runner);
        var directory = Path.GetDirectoryName(composeFile)!;
        try
        {
            File.Exists(composeFile).ShouldBeTrue();
            Path.GetDirectoryName(directory).ShouldBe(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(composeFile).ShouldBe(OwnerReadWrite);
                Directory.GetParent(composeFile)!.UnixFileMode.ShouldBe(OwnerAll);
            }
        }
        finally
        {
            await provider.TeardownAsync(result.Descriptor.Id, ct);
        }

        File.Exists(composeFile).ShouldBeFalse();
        Directory.Exists(directory).ShouldBeFalse();
    }

    /// <summary>A provision that fails after the file was written removes both the file and its private directory.</summary>
    [Fact]
    public async Task FailedProvisionRemovesTheComposeFileAndItsDirectory()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false });
        runner.SetResult(PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner, options: new SandboxProviderOptions { PerSandboxCredentials = false });
        var ct = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        var composeFile = ComposeFileOf(runner);
        File.Exists(composeFile).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(composeFile)!).ShouldBeFalse();
    }

    private static string ComposeFileOf(FakeProcessRunner runner) =>
        runner.Specs.First(static s => PodmanTestData.HasArgument(s, "up"))
            .Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
}
