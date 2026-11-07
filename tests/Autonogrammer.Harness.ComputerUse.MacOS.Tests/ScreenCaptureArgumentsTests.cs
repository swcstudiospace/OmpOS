using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.MacOS.Tests;

/// <summary>Argument vector and environment allow-list. No process is started.</summary>
public sealed class ScreenCaptureArgumentsTests
{
    [Fact]
    public void VectorIsSilentPngWithOutfileUnderStateDirectory()
    {
        IReadOnlyList<string> args = ScreenCaptureArguments.Create("/var/autonogrammer/state", "frame.png");
        args.Count.ShouldBe(4);
        args[0].ShouldBe("-x");
        args[1].ShouldBe("-t");
        args[2].ShouldBe("png");
        args[3].ShouldBe(Path.Combine("/var/autonogrammer/state", "frame.png"));
        string.Join(' ', args).ShouldNotContain("sh ");
        ScreenCaptureArguments.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
        ScreenCaptureArguments.Executable.ShouldBe("/usr/sbin/screencapture");
    }

    [Fact]
    public void EnvironmentKeepsOnlyTheAllowList()
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = "/Users/example",
            ["TMPDIR"] = "/tmp",
            ["USER"] = "example",
            ["LOGNAME"] = "example",
            ["LANG"] = "en_US.UTF-8",
            ["LC_ALL"] = "en_US.UTF-8",
            ["LC_CTYPE"] = "UTF-8",
            ["OMP_API_KEY"] = "drop-me",
            ["HTTPS_PROXY"] = "http://proxy.example",
            ["PATH"] = "/usr/bin",
            ["OPENAI_API_KEY"] = "drop-provider",
        };

        IReadOnlyDictionary<string, string> kept = ScreenCaptureArguments.ScreenCaptureEnvironment(source);
        kept.Keys.ShouldBe(["HOME", "TMPDIR", "USER", "LOGNAME", "LANG", "LC_ALL", "LC_CTYPE"], ignoreOrder: true);
        kept.ShouldNotContainKey("OMP_API_KEY");
        kept.ShouldNotContainKey("HTTPS_PROXY");
        kept.ShouldNotContainKey("PATH");
        kept.ShouldNotContainKey("OPENAI_API_KEY");
        kept["HOME"].ShouldBe("/Users/example");
    }
}
