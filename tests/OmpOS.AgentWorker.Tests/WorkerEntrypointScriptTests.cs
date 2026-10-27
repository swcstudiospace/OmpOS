using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>
/// Runs <c>deploy/worker-entrypoint.sh</c> under <c>sh</c> with stub <c>id</c>, <c>stat</c>, <c>chown</c> and <c>setpriv</c> on a private PATH and
/// a temporary directory as the volume, so every branch is observed through what the script asks the stubs to do.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class WorkerEntrypointScriptTests
{
    /// <summary>As root with wrong ownership the script repairs the mount and the state trees, then drops privileges and execs the program.</summary>
    [Fact]
    public async Task RootRepairsOwnershipThenDropsPrivileges()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: true);

        var result = await sandbox.RunAsync(["worker-program", "--flag", "value with space"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0, result.Stderr);
        sandbox.ChownCalls.ShouldBe(
            new[]
            {
                $"10001:10001 {sandbox.Volume}",
                $"-R 10001:10001 {sandbox.Home}",
                $"-R 10001:10001 {sandbox.Volume}/work",
                $"-R 10001:10001 {sandbox.Volume}/ompos",
                $"10001:10001 {sandbox.Home}/.omp",
            });
        sandbox.SetprivCalls.ShouldBe(new[] { "--reuid=10001 --regid=10001 --clear-groups --no-new-privs -- worker-program --flag value with space" });
        Directory.Exists(Path.Combine(sandbox.Volume, "work")).ShouldBeTrue();
        Directory.Exists(Path.Combine(sandbox.Volume, "ompos")).ShouldBeTrue();
        Directory.Exists(Path.Combine(sandbox.Home, ".omp")).ShouldBeTrue();
    }

    /// <summary>When the state trees already belong to the run user only the mount point is repaired, so a woken worker never walks a big volume.</summary>
    [Fact]
    public async Task RootDoesNotWalkTreesThatAlreadyBelongToTheRunUser()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 10001, withSetpriv: true);

        var result = await sandbox.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0, result.Stderr);
        sandbox.ChownCalls.ShouldBe(new[] { $"10001:10001 {sandbox.Volume}" });
        sandbox.SetprivCalls.Length.ShouldBe(1);
    }

    /// <summary>A custom uid and gid reach setpriv and chown.</summary>
    [Fact]
    public async Task CustomUidAndGidAreUsed()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: true);
        sandbox.Environment["OMPOS_RUN_UID"] = "2000";
        sandbox.Environment["OMPOS_RUN_GID"] = "3000";

        var result = await sandbox.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0, result.Stderr);
        sandbox.ChownCalls[0].ShouldBe($"2000:3000 {sandbox.Volume}");
        sandbox.SetprivCalls.Single().ShouldStartWith("--reuid=2000 --regid=3000 ", Case.Sensitive);
    }

    /// <summary>A volume path that is not a directory stops a root start with exit 66 before anything is changed.</summary>
    [Fact]
    public async Task MissingVolumeExits66WithoutChangingAnything()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: true);
        sandbox.Environment["OMPOS_VOLUME_PATH"] = Path.Combine(sandbox.Root, "no-such-volume");

        var result = await sandbox.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(66);
        result.Stderr.ShouldContain("not an existing directory", Case.Sensitive);
        sandbox.ChownCalls.ShouldBeEmpty();
        sandbox.SetprivCalls.ShouldBeEmpty();
    }

    /// <summary>Without setpriv a root start refuses to run the program as root and exits 69.</summary>
    [Fact]
    public async Task MissingSetprivExits69()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: false);

        var result = await sandbox.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(69);
        result.Stderr.ShouldContain("setpriv not found", Case.Sensitive);
        sandbox.ChownCalls.ShouldBeEmpty();
    }

    /// <summary>Invalid uid or gid values are refused with exit 64.</summary>
    /// <param name="variable">The variable that is set.</param>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData("OMPOS_RUN_UID", "abc")]
    [InlineData("OMPOS_RUN_UID", "0")]
    [InlineData("OMPOS_RUN_UID", "-5")]
    [InlineData("OMPOS_RUN_UID", "010")]
    [InlineData("OMPOS_RUN_GID", "1x")]
    [InlineData("OMPOS_RUN_GID", "0")]
    public async Task InvalidIdentityExits64(string variable, string value)
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: true);
        sandbox.Environment[variable] = value;

        var result = await sandbox.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(64);
        sandbox.SetprivCalls.ShouldBeEmpty();
    }

    /// <summary>As a non-root user the script skips the repair and execs the program directly, with the listen address and home set.</summary>
    [Fact]
    public async Task NonRootExecsTheProgramDirectlyWithTheListenAddress()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 1000, statUid: 0, withSetpriv: true);
        sandbox.Environment["PORT"] = "9123";
        sandbox.Environment["ASPNETCORE_HTTP_PORTS"] = "5000";

        var result = await sandbox.RunAsync(["probe-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0, result.Stderr);
        result.Stdout.ShouldContain("ASPNETCORE_URLS=http://[::]:9123", Case.Sensitive);
        result.Stdout.ShouldContain("ASPNETCORE_HTTP_PORTS=unset", Case.Sensitive);
        result.Stdout.ShouldContain("HOME=" + sandbox.Home, Case.Sensitive);
        sandbox.ChownCalls.ShouldBeEmpty();
        sandbox.SetprivCalls.ShouldBeEmpty();
    }

    /// <summary>The listen port defaults to 8080.</summary>
    [Fact]
    public async Task ListenPortDefaultsTo8080()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 1000, statUid: 0, withSetpriv: false);

        var result = await sandbox.RunAsync(["probe-program"], timeout.Token).ConfigureAwait(true);

        result.Stdout.ShouldContain("ASPNETCORE_URLS=http://[::]:8080", Case.Sensitive);
    }

    /// <summary>A non-root start on a volume it cannot write exits 73.</summary>
    [Fact]
    public async Task NonRootWithoutWritableVolumeExits73()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 1000, statUid: 0, withSetpriv: false);
        sandbox.Environment["OMPOS_VOLUME_PATH"] = Path.Combine(sandbox.Root, "no-such-volume");

        var result = await sandbox.RunAsync(["probe-program"], timeout.Token).ConfigureAwait(true);

        result.ExitCode.ShouldBe(73);
    }

    /// <summary>The script never prints environment values: a planted secret appears on neither output stream.</summary>
    [Fact]
    public async Task SecretEnvironmentValuesAreNeverPrinted()
    {
        using var timeout = TestTimeouts.Create();
        using var sandbox = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: true);
        using var invalid = new EntrypointSandbox(effectiveUid: 0, statUid: 0, withSetpriv: true);
        var canary = "canary-secret-" + Guid.NewGuid().ToString("N");
        sandbox.Environment["ANTHROPIC_API_KEY"] = canary;
        invalid.Environment["ANTHROPIC_API_KEY"] = canary;
        invalid.Environment["OMPOS_RUN_UID"] = "bad";
        var ok = await sandbox.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);
        var refused = await invalid.RunAsync(["worker-program"], timeout.Token).ConfigureAwait(true);

        (ok.Stdout + ok.Stderr + refused.Stdout + refused.Stderr).ShouldNotContain(canary, Case.Sensitive);
    }

    private sealed record ScriptResult(int ExitCode, string Stdout, string Stderr);

    private sealed class EntrypointSandbox : IDisposable
    {
        private readonly string _bin;
        private readonly string _log;

        public EntrypointSandbox(int effectiveUid, int statUid, bool withSetpriv)
        {
            LinuxOnly.Require();
            Root = Directory.CreateTempSubdirectory("ompos-entrypoint-").FullName;
            _bin = Path.Combine(Root, "bin");
            Directory.CreateDirectory(_bin);
            Volume = Path.Combine(Root, "volume");
            Directory.CreateDirectory(Volume);
            Home = Path.Combine(Volume, "home");
            _log = Path.Combine(Root, "calls.log");
            File.WriteAllText(_log, string.Empty);
            File.CreateSymbolicLink(Path.Combine(_bin, "mkdir"), ResolveRealMkdir());
            WriteStub("id", $"printf '%s\\n' {effectiveUid}");
            WriteStub("stat", $"printf '%s\\n' {statUid}");
            WriteStub("chown", "printf 'chown %s\\n' \"$*\" >> \"$STUB_LOG\"");
            WriteStub("probe-program", "printf 'ASPNETCORE_URLS=%s\\nASPNETCORE_HTTP_PORTS=%s\\nHOME=%s\\n' \"$ASPNETCORE_URLS\" \"${ASPNETCORE_HTTP_PORTS-unset}\" \"$HOME\"");
            if (withSetpriv)
            {
                WriteStub("setpriv", "printf 'setpriv %s\\n' \"$*\" >> \"$STUB_LOG\"");
            }

            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = _bin,
                ["OMPOS_VOLUME_PATH"] = Volume,
                ["HOME"] = Home,
                ["STUB_LOG"] = _log,
            };
        }

        public string Root { get; }

        public string Volume { get; }

        public string Home { get; }

        public Dictionary<string, string> Environment { get; }

        public string[] ChownCalls => Calls("chown ");

        public string[] SetprivCalls => Calls("setpriv ");

        public async Task<ScriptResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            var script = Path.Combine(FindRepositoryRoot(), "deploy", "worker-entrypoint.sh");
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            startInfo.ArgumentList.Add(script);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.Environment.Clear();
            foreach (var (name, value) in Environment)
            {
                startInfo.Environment[name] = value;
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("sh did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new ScriptResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private string[] Calls(string prefix) =>
            [.. File.ReadAllLines(_log).Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).Select(line => line[prefix.Length..])];

        private void WriteStub(string name, string body)
        {
            var path = Path.Combine(_bin, name);
            File.WriteAllText(path, "#!/bin/sh\n" + body + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        private static string ResolveRealMkdir() =>
            new[] { "/bin/mkdir", "/usr/bin/mkdir" }.First(File.Exists);

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("global.json was not found above the test output.");
        }
    }
}
