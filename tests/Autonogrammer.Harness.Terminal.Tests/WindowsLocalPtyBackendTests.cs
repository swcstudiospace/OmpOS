using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// Verifies the Windows environment policy without a Windows host: the change set holds only the caller's variables,
/// an explicit name replaces a host name of the same spelling, an empty value is kept so the pseudo-terminal removes
/// that name, and a value never appears in an exception. A start on this Linux host is refused after those checks.
/// </summary>
public sealed class WindowsLocalPtyBackendTests
{
    private const string SecretValue = "s3cret-value-1234";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void EnvironmentChangeKeepsOnlyTheCallersVariables()
    {
        const string sentinel = "AUTONOGRAMMER_TERMINAL_ENV_TEST_WIN_LEAK";
        Environment.SetEnvironmentVariable(sentinel, "leaked");
        try
        {
            var change = WindowsLocalPtyBackend.EnvironmentChange(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["OMP_PROFILE"] = "desk" });

            change.Count.ShouldBe(1);
            change["OMP_PROFILE"].ShouldBe("desk");
            change.ContainsKey(sentinel).ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(sentinel, null);
        }
    }

    [Fact]
    public void EnvironmentChangeCopiesTheCallersEntriesAndKeepsAnEmptyValue()
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "from options",
            ["DROP_ME"] = string.Empty,
        };

        var change = WindowsLocalPtyBackend.EnvironmentChange(source);
        source["PATH"] = "mutated after the copy";

        change["PATH"].ShouldBe("from options");
        change["DROP_ME"].ShouldBe(string.Empty);
    }

    [Fact]
    public async Task StartAsyncRefusesThisHostWhenItIsNotWindows()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The refusal under test is the non-Windows path.");
        using var workspace = new TempWorkspace();

        var exception = await Should.ThrowAsync<PlatformNotSupportedException>(() => new WindowsLocalPtyBackend().StartAsync(
            new TerminalSessionOptions("/bin/sh", ["-c", "exit 0"], workspace.Path, 80, 24, new RecordingSink()),
            Token)).ConfigureAwait(true);

        exception.Message.ShouldContain("Windows");
    }

    [Fact]
    public async Task AnInvalidEnvironmentIsRefusedWithoutPrintingTheValue()
    {
        using var workspace = new TempWorkspace();

        var exception = await Should.ThrowAsync<ArgumentException>(() => new WindowsLocalPtyBackend().StartAsync(
            new TerminalSessionOptions(
                "/bin/sh",
                ["-c", "exit 0"],
                workspace.Path,
                80,
                24,
                new RecordingSink())
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GOOD"] = SecretValue + "\0" },
            },
            Token)).ConfigureAwait(true);

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldNotContain(SecretValue);
    }

    [Fact]
    public void CurrentOperatingSystemSelectsTheBackendForThisHost()
    {
        var backend = LocalPtyBackend.ForCurrentOperatingSystem();
        if (OperatingSystem.IsWindows())
        {
            backend.ShouldBeOfType<WindowsLocalPtyBackend>();
        }
        else if (OperatingSystem.IsLinux())
        {
            backend.ShouldBeOfType<LinuxLocalPtyBackend>();
        }
        else
        {
            throw new PlatformNotSupportedException("This test runs on Windows or Linux.");
        }
    }
}
