using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// The file scripts also run in minimal containers whose <c>realpath</c> is the BusyBox one: it takes no <c>--</c> (it reports the path it was given and a failure) and
/// refuses a path that does not exist yet. Both used to make every file creation in an Alpine container fail as "cannot confirm where the file lies".
/// </summary>
public sealed class RemoteScriptsBusyBoxTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A <c>realpath</c> that behaves like BusyBox's: no <c>--</c> option, and only paths that exist.</summary>
    private const string BusyBoxRealpath = """
        #!/bin/sh
        rc=0
        for a; do
          if [ "$a" != "--" ] && [ -e "$a" ]; then
            readlink -f -- "$a"
          else
            printf 'realpath: %s: No such file or directory\n' "$a" >&2
            rc=1
          fi
        done
        exit $rc

        """;

    /// <summary>A new file, in a directory that does not exist yet, is created through a <c>realpath</c> like BusyBox's, and the content is the bytes sent.</summary>
    [Fact]
    public async Task ACreationWorksWithARealpathThatTakesNoDoubleDashAndNeedsTheFileToExist()
    {
        using var directory = TempDirectory.Create();
        var bin = directory.Combine("bin");
        _ = Directory.CreateDirectory(bin);
        FakeGuestFiles.WriteExecutable(Path.Combine(bin, "realpath"), BusyBoxRealpath);
        var workspace = directory.Combine("work");
        _ = Directory.CreateDirectory(workspace);
        var file = Path.Combine(workspace, "new", "deep", "f.txt");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = bin + ":/usr/bin:/bin" };

        var result = await ShellRunner.RunShAsync(RemoteScripts.Create(workspace, file), "made"u8.ToArray(), environment, Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0, result.StandardError);
        (await File.ReadAllTextAsync(file, Token).ConfigureAwait(true)).ShouldBe("made");
    }

    /// <summary>A link that leads out of the workspace is still refused when <c>realpath</c> is the BusyBox one.</summary>
    [Fact]
    public async Task ALinkOutOfTheWorkspaceIsStillRefusedWithThatRealpath()
    {
        using var directory = TempDirectory.Create();
        var bin = directory.Combine("bin");
        _ = Directory.CreateDirectory(bin);
        FakeGuestFiles.WriteExecutable(Path.Combine(bin, "realpath"), BusyBoxRealpath);
        var workspace = directory.Combine("work");
        var outside = directory.Combine("outside");
        _ = Directory.CreateDirectory(workspace);
        _ = Directory.CreateDirectory(outside);
        File.CreateSymbolicLink(Path.Combine(workspace, "escape"), outside);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = bin + ":/usr/bin:/bin" };

        var result = await ShellRunner.RunShAsync(RemoteScripts.Create(workspace, Path.Combine(workspace, "escape", "stolen.txt")), "x"u8.ToArray(), environment, Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(RemoteScripts.ExitEscapes);
        File.Exists(Path.Combine(outside, "stolen.txt")).ShouldBeFalse();
    }
}
