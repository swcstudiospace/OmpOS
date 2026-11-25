using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;
namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Opt-in capture of <c>omp --mode rpc</c> plugin loading (Phase 82 Task 1, no live turn).
/// Starts the real <c>omp</c> with the full planned argv (transport words plus the ACP-shaped contribution
/// arguments: <c>--no-extensions</c>/<c>-e</c> entries, <c>--config</c> overlay, <c>--skills</c>,
/// <c>--add-dir</c>, <c>--append-system-prompt</c>, <c>--approval-mode</c>), drives only the handshake
/// (<c>negotiate_protocol</c> + <c>get_state</c> inside <see cref="OmpRpcSession.CreateAsync"/>), never sends a
/// <c>prompt</c>, then closes stdin. It records every stdout frame line under <c>TestData/frames</c> and asserts
/// the <c>ready</c> frame, the unsolicited <c>available_commands_update</c> plugin commands, and the
/// <c>gsd-core-omp</c> connection report naming mode <c>rpc</c>. Isolated under a throwaway <c>HOME</c> with
/// credential-shaped variables dropped, <c>--no-session</c> and a throwaway <c>--session-dir</c>.
/// Skipped unless <c>AUTONOGRAMMER_RPC_LIVE_CAPTURE=1</c> and an <c>omp</c> executable is on PATH, so a plain
/// test run never spawns a real omp process.
/// </summary>
[Trait("Category", "Live")]
public sealed class RpcPluginLoadCaptureTests
{
    private const string CaptureVariable = "AUTONOGRAMMER_RPC_LIVE_CAPTURE";
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CommandsTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReportTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets whether the capture is requested and an <c>omp</c> executable is on PATH. Gates the capture.</summary>
    public static bool CaptureEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(CaptureVariable), "1", StringComparison.Ordinal)
        && RpcLiveSmokeTests.OmpOnPath;

    /// <summary>Starts <c>omp --mode rpc</c> with the full planned argv and records handshake-level frames only.</summary>
    [Fact(SkipUnless = nameof(CaptureEnabled), Skip = "Set AUTONOGRAMMER_RPC_LIVE_CAPTURE=1 and put omp on PATH to run the plugin-load capture.")]
    public async Task PluginLoadCaptureRecordsHandshakeFramesWithoutAPrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), $"autonogrammer-rpc-plugin-load-{Guid.NewGuid():N}");
        string reportPath = Path.Combine(root, "state", "connection", "report.json");
        string framesPath = FramesPath();
        if (!PluginEntriesPresent())
        {
            Assert.Skip("A plugin entry is not initialised; the plugin-load capture is skipped.");
        }

        RawProcessTransport transport = CreateFixture(root, framesPath);
        OmpRpcSession session = new(transport, HandshakeTimeout, CommandTimeout);
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);
            session.State.ShouldBe(HarnessState.Ready);

            await WaitForPluginCommandsAsync(session, ct).ConfigureAwait(true);

            JsonElement state = await session.GetStateAsync(ct).ConfigureAwait(true);
            state.TryGetProperty("model", out _).ShouldBeTrue("get_state returned no model.");

            await WaitForConnectionReportAsync(reportPath, ct).ConfigureAwait(true);

            transport.SentLines.Any(static line => line.Contains("\"prompt\"", StringComparison.Ordinal)).ShouldBeFalse("the capture must never send a prompt.");

            await transport.CloseInputAsync(ct).ConfigureAwait(true);
            int exitCode = await transport.WaitForExitAsync(ct).ConfigureAwait(true);
            exitCode.ShouldBe(0);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            await transport.DisposeAsync().ConfigureAwait(true);
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of the throwaway root; the capture file is the retained evidence.
            }
        }

        new FileInfo(framesPath).Length.ShouldBeGreaterThan(0L);
    }

    private static bool PluginEntriesPresent()
    {
        string pluginsRoot = Path.Combine(RepoRoot(), "plugins");
        return Directory.Exists(Path.Combine(pluginsRoot, "claude-ultrathink"))
            && Directory.Exists(Path.Combine(pluginsRoot, "agent-swarm", "omp"))
            && Directory.Exists(Path.Combine(pluginsRoot, "gsd-core-omp"));
    }

    private static RawProcessTransport CreateFixture(string root, string framesPath)
    {
        string home = Path.Combine(root, "home");
        string workspace = Path.Combine(root, "workspace");
        string addDir = Path.Combine(root, "adddir");
        string stateDir = Path.Combine(root, "state");
        string sessionsDir = Path.Combine(root, "sessions");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(addDir);
        Directory.CreateDirectory(Path.Combine(stateDir, "connection"));
        Directory.CreateDirectory(Path.Combine(stateDir, "steer"));
        Directory.CreateDirectory(sessionsDir);

        string pluginsRoot = Path.Combine(RepoRoot(), "plugins");
        string ultrathink = Path.Combine(pluginsRoot, "claude-ultrathink");
        string swarm = Path.Combine(pluginsRoot, "agent-swarm", "omp");
        string gsd = Path.Combine(pluginsRoot, "gsd-core-omp");

        string configOverlay = Path.Combine(root, "overlay.yml");
        File.WriteAllText(
            configOverlay,
            "mcp:\n  enableProjectConfig: false\nworkspace:\n  additionalDirectories:\n    - \"" + addDir + "\"\nskills:\n  customDirectories:\n    - \"" + Path.Combine(root, "skills-pack") + "\"\n");
        Directory.CreateDirectory(Path.Combine(root, "skills-pack", "fixture-skill"));
        File.WriteAllText(Path.Combine(root, "skills-pack", "fixture-skill", "SKILL.md"), "# fixture skill\n");
        string systemPrompt = Path.Combine(root, "sysprompt.txt");
        File.WriteAllText(systemPrompt, "fixture standing context\n");

        Dictionary<string, string> environment = BuildEnvironment(home, stateDir);
        List<string> arguments =
        [
            "--mode", "rpc",
            "--no-ui",
            "--cwd", workspace,
            "--session-dir", sessionsDir,
            "--no-session",
            "--model", "opus",
            "--no-extensions",
            "-e", ultrathink,
            "-e", swarm,
            "-e", gsd,
            "--config", configOverlay,
            "--skills", "fixture-*",
            "--add-dir", addDir,
            "--append-system-prompt", systemPrompt,
            "--approval-mode", "write",
        ];

        return RawProcessTransport.Start("omp", workspace, arguments, framesPath, environment);
    }

    private static Dictionary<string, string> BuildEnvironment(string home, string stateDir) => new(StringComparer.Ordinal)
    {
        ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin",
        ["HOME"] = home,
        ["XDG_CONFIG_HOME"] = Path.Combine(home, ".config"),
        ["XDG_DATA_HOME"] = Path.Combine(home, ".local", "share"),
        ["XDG_CACHE_HOME"] = Path.Combine(home, ".cache"),
        ["LANG"] = "C.UTF-8",
        ["ULTRATHINK_STATE_DIR"] = Path.Combine(stateDir, "ultrathink"),
        ["SWARM_DIR"] = Path.Combine(stateDir, "swarm"),
        ["SWARM_GSD_STATE_FILE"] = Path.Combine(stateDir, "swarm", "gsd-state.md"),
        ["SWARM_CLAUDE_STATE_DIR"] = Path.Combine(stateDir, "claude-state"),
        ["SUBSTRATE_OMP_PENDING_DIR"] = Path.Combine(stateDir, "substrate-pending"),
        ["AUTONOGRAMMER_STEER_DIR"] = Path.Combine(stateDir, "steer"),
        ["AUTONOGRAMMER_CONNECTION_FILE"] = Path.Combine(stateDir, "connection", "report.json"),
        ["ULTRATHINK_UPLIFT"] = "0",
    };

    private static string FramesPath()
    {
        string framesDirectory = Path.Combine(RepoRoot(), "tests", "Autonogrammer.Harness.Rpc.Tests", "TestData", "frames");
        Directory.CreateDirectory(framesDirectory);
        string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        return Path.Combine(framesDirectory, $"rpc-plugin-load-{stamp}.ndjson");
    }

    private static async Task WaitForPluginCommandsAsync(OmpRpcSession session, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + CommandsTimeout;
        while (DateTime.UtcNow < deadline)
        {
            HashSet<string> names = new(session.Commands.Select(static command => command.Name), StringComparer.Ordinal);
            if (names.Contains("gsd-core", StringComparer.Ordinal) && names.Contains("swarm", StringComparer.Ordinal) && names.Contains("ultrathink-on", StringComparer.Ordinal))
            {
                return;
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(true);
        }

        throw new TimeoutException("available_commands_update did not name the loaded plugins' commands within " + CommandsTimeout.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s.");
    }

    private static async Task WaitForConnectionReportAsync(string reportPath, CancellationToken ct)
    {
        DateTime deadline = DateTime.UtcNow + ReportTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(reportPath))
            {
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath, ct).ConfigureAwait(true));
                JsonElement root = document.RootElement;
                root.TryGetProperty("mode", out JsonElement mode).ShouldBeTrue("the connection report names no mode.");
                mode.GetString().ShouldBe("rpc");
                root.TryGetProperty("commandsAvailable", out JsonElement available).ShouldBeTrue("the connection report names no commandsAvailable flag.");
                available.GetBoolean().ShouldBeTrue("the connection report says its commands are unavailable.");
                root.TryGetProperty("commands", out JsonElement commands).ShouldBeTrue("the connection report lists no commands.");
                List<string> names = commands.EnumerateArray().Select(static command => command.GetString() ?? string.Empty).ToList();
                names.Contains("gsd-core", StringComparer.Ordinal).ShouldBeTrue("the connection report does not name gsd-core.");
                return;
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(true);
        }

        throw new TimeoutException("The gsd-core-omp connection report did not appear within " + ReportTimeout.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) + " s.");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Autonogrammer.slnx was not found above the test output directory.");
    }

    /// <summary>
    /// A line transport over a directly spawned <c>omp --mode rpc</c> child with the full planned argv
    /// (<see cref="OmpRpcProcess"/> does not take extra arguments yet), mirroring its caps: one physical line per
    /// frame, a bounded stderr tail, EOF shutdown with a ten-second grace. Every stdout line is copied to the
    /// capture file exactly as received before the session sees it.
    /// </summary>
    private sealed class RawProcessTransport : IRpcTransport
    {
        private const int StderrTailLines = 200;

        private readonly Process _process;
        private readonly StreamWriter _frames;
        private readonly ConcurrentQueue<string> _stderrTail = new();
        private readonly List<string> _sentLines = [];
        private readonly Lock _gate = new();
        private readonly Task _stderrDrain;
        private bool _inputClosed;
        private bool _disposed;

        private RawProcessTransport(Process process, string framesPath)
        {
            _process = process;
            _frames = new StreamWriter(framesPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };
            _stderrDrain = Task.Run(DrainStderrAsync, CancellationToken.None);
        }

        /// <summary>Starts <c>omp</c> with the planned argv, an isolated environment, and frame capture to disk.</summary>
        /// <param name="ompPath">The omp executable, resolved through PATH.</param>
        /// <param name="cwd">The child's working directory.</param>
        /// <param name="arguments">The argument list after <c>omp</c>.</param>
        /// <param name="framesPath">Where every stdout frame line is copied as received.</param>
        /// <param name="environment">The child's complete environment (already credential-scrubbed by the caller).</param>
        /// <returns>The running transport.</returns>
        public static RawProcessTransport Start(
            string ompPath,
            string cwd,
            IReadOnlyList<string> arguments,
            string framesPath,
            IReadOnlyDictionary<string, string> environment)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = ompPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = cwd,
            };
            startInfo.Environment.Clear();
            foreach (KeyValuePair<string, string> entry in environment)
            {
                startInfo.Environment[entry.Key] = entry.Value;
            }

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start '{ompPath}'.");
            return new RawProcessTransport(process, framesPath);
        }

        /// <summary>Gets the command lines written so far, oldest first.</summary>
        public IReadOnlyList<string> SentLines
        {
            get
            {
                lock (_gate)
                {
                    return _sentLines.ToArray();
                }
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                await _frames.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                yield return line;
            }
        }

        /// <inheritdoc />
        public async Task SendLineAsync(string line, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(line);
            lock (_gate)
            {
                _sentLines.Add(line);
            }

            await _process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task CloseInputAsync(CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                if (_inputClosed)
                {
                    return;
                }

                _inputClosed = true;
            }

            try
            {
                await _process.StandardInput.DisposeAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The engine already exited and its stdin pipe is gone; the close goal is met.
            }
        }

        /// <inheritdoc />
        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return _process.ExitCode;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await CloseInputAsync(CancellationToken.None).ConfigureAwait(false);
            using CancellationTokenSource grace = new(ShutdownGrace);
            try
            {
                await _process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (grace.IsCancellationRequested)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the grace timeout and the kill.
                }

                await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await _frames.DisposeAsync().ConfigureAwait(false);
            _process.Dispose();
        }


        private async Task DrainStderrAsync()
        {
            try
            {
                while (await _process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    _stderrTail.Enqueue(line);
                    while (_stderrTail.Count > StderrTailLines)
                    {
                        _ = _stderrTail.TryDequeue(out _);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The pipe closed with the process; the tail already holds everything it wrote.
            }
        }
    }
}
