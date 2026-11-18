using System.Runtime.Versioning;
using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>The stderr drain of <see cref="AcpStdioTransport"/> keeps a bounded line: an unterminated run from the child is cut, and the next line still arrives.</summary>
public sealed class AcpStdioStderrLineTests
{
    /// <summary>A stderr line far longer than the cap is kept only up to the cap, and the line after it is delivered whole.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AnOverLongStderrLineIsCutAndTheNextLineStillArrives()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The stdio fixture uses a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        var directory = CreateDirectory();
        try
        {
            var script = WriteScript(directory, "noisy-stderr", "head -c 20000 /dev/zero | tr '\\0' 'a' 1>&2\necho 1>&2\necho 'after' 1>&2\n");
            var transport = new AcpStdioTransport(script, directory);
            await using var transportScope = transport.ConfigureAwait(true);
            await transport.ConnectAsync(ct).ConfigureAwait(true);

            var kept = await transport.StandardError.ReadAsync(ct).ConfigureAwait(true);
            kept.ShouldBe(new string('a', 4096));
            (await transport.StandardError.ReadAsync(ct).ConfigureAwait(true)).ShouldBe("after");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "acp-stderr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [SupportedOSPlatform("linux")]
    private static string WriteScript(string directory, string name, string body)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }
}
