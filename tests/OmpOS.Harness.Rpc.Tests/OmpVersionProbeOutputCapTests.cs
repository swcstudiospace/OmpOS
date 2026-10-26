using System.Diagnostics;
using System.Runtime.Versioning;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>The version probe caps captured stdout: output past 4096 characters kills the child and fails the probe.</summary>
public sealed class OmpVersionProbeOutputCapTests
{
    /// <summary>Output over the cap fails the probe with a typed process-start failure, and the child is killed rather than awaited.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task OutputOverTheCapKillsTheChildAndFailsTheProbe()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            // The child prints 5,000 characters, then sleeps for a minute. Only a kill ends the probe early.
            var script = WriteScript(directory, "flood", "head -c 5000 /dev/zero | tr '\\0' 'v'\nsleep 60\n");
            var clock = Stopwatch.StartNew();

            var failure = await Should.ThrowAsync<ProcessStartHarnessException>(
                () => OmpVersionProbe.ProbeAsync(script, ct)).ConfigureAwait(true);

            clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
            failure.Message.ShouldContain("more than 4096 characters");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "omp-version-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string name, string body)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
