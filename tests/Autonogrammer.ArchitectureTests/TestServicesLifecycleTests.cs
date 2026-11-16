using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

public sealed class TestServicesLifecycleTests
{
    private const string Postgres = "autonogrammer-test-postgres-lane";
    private const string Dragonfly = "autonogrammer-test-dragonfly-lane";
    private const string Greptime = "autonogrammer-test-greptimedb-lane";
    private static readonly string Owner = new('a', 64);
    private static readonly string OtherOwner = new('b', 64);

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    public void SameNameFromAnotherRunIsNeverRemoved(string command)
    {
        using var fixture = new PodmanFixture();
        fixture.Add(Postgres, 'c', OtherOwner);
        var result = fixture.Run(command, Owner);
        result.ExitCode.ShouldNotBe(0);
        File.Exists(Path.Combine(fixture.State, Postgres)).ShouldBeTrue();
        File.Exists(Path.Combine(fixture.State, "runs")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.State, "removals")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    public void EngineErrorsFailClosed(string command)
    {
        using var fixture = new PodmanFixture();
        File.WriteAllText(Path.Combine(fixture.State, "exists-error"), Postgres);
        fixture.Run(command, Owner).ExitCode.ShouldNotBe(0);
        File.Exists(Path.Combine(fixture.State, "runs")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.State, "removals")).ShouldBeFalse();
    }

    [Fact]
    public void InspectErrorDoesNotRemoveContainer()
    {
        using var fixture = new PodmanFixture();
        fixture.Add(Postgres, 'c', Owner);
        File.WriteAllText(Path.Combine(fixture.State, "inspect-error"), Postgres);
        fixture.Run("down", Owner).ExitCode.ShouldNotBe(0);
        File.Exists(Path.Combine(fixture.State, Postgres)).ShouldBeTrue();
        File.Exists(Path.Combine(fixture.State, "removals")).ShouldBeFalse();
    }

    [Fact]
    public void DownRemovesOnlyMatchingRunAndIsIdempotent()
    {
        using var fixture = new PodmanFixture();
        fixture.Add(Postgres, 'c', Owner);
        fixture.Add(Dragonfly, 'd', Owner);
        fixture.Add(Greptime, 'e', OtherOwner);
        fixture.Run("down", Owner).ExitCode.ShouldNotBe(0);
        fixture.Run("down", Owner).ExitCode.ShouldNotBe(0);
        File.ReadAllLines(Path.Combine(fixture.State, "removals")).Length.ShouldBe(2);
        File.Exists(Path.Combine(fixture.State, Greptime)).ShouldBeTrue();
    }

    [Fact]
    public async Task UpWaitsForReadinessAndCiDownUsesExportFile()
    {
        using var fixture = new PodmanFixture();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            var buffer = new byte[512];
            var count = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
            Encoding.ASCII.GetString(buffer, 0, count).ShouldContain("AUTH ");
            await stream.WriteAsync("+OK\r\n+PONG\r\n"u8.ToArray(), timeout.Token).ConfigureAwait(false);
        }, timeout.Token);

        var up = fixture.Run("up", port: port);
        up.ExitCode.ShouldBe(0, up.Error);
        await server.ConfigureAwait(true);
        var execs = await File.ReadAllLinesAsync(Path.Combine(fixture.State, "execs"), timeout.Token).ConfigureAwait(true);
        execs.Length.ShouldBeGreaterThanOrEqualTo(2);
        up.Error.ShouldNotContain("container log secret");
        var envFile = up.Output.Trim();
        File.Exists(envFile).ShouldBeTrue();
        try
        {
            fixture.Run("down", envFile: envFile).ExitCode.ShouldBe(0);
            fixture.Run("down", envFile: envFile).ExitCode.ShouldBe(0);
            File.Exists(Path.Combine(fixture.State, Postgres)).ShouldBeFalse();
            File.Exists(Path.Combine(fixture.State, Dragonfly)).ShouldBeFalse();
        }
        finally
        {
            File.Delete(envFile);
        }
    }

    private sealed class PodmanFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "test-services-fixture-" + Guid.NewGuid().ToString("N"));
        public string State => Path.Combine(root, "state");

        public PodmanFixture()
        {
            if (OperatingSystem.IsWindows()) Assert.Skip("The test-services script requires a Unix shell.");
            Directory.CreateDirectory(State);
            var fake = Path.Combine(root, "podman");
            File.WriteAllText(fake, """
                #!/usr/bin/env bash
                case "$1" in
                  container)
                    [ "$(cat "$FAKE_STATE/exists-error" 2>/dev/null)" != "$3" ] || exit 125
                    [ -f "$FAKE_STATE/$3" ]
                    ;;
                  inspect)
                    [ "$(cat "$FAKE_STATE/inspect-error" 2>/dev/null)" != "$4" ] || exit 125
                    cat "$FAKE_STATE/$4"
                    ;;
                  rm)
                    echo "$3" >> "$FAKE_STATE/removals"
                    for file in "$FAKE_STATE"/autonogrammer-test-*; do
                      [ -f "$file" ] || continue
                      read -r id owner < "$file"
                      [ "$id" != "$3" ] || rm "$file"
                    done
                    ;;
                  run)
                    echo run >> "$FAKE_STATE/runs"
                    name="" label=""
                    while [ "$#" -gt 0 ]; do
                      case "$1" in
                        --name) name="$2"; shift ;;
                        --label) label="${2#*=}"; shift ;;
                      esac
                      shift
                    done
                    [ ! -e "$FAKE_STATE/$name" ] || exit 125
                    if [[ "$name" == *postgres* ]]; then id=$(printf 'c%.0s' {1..64}); else id=$(printf 'd%.0s' {1..64}); fi
                    echo "$id $label" > "$FAKE_STATE/$name"
                    ;;
                  port) echo "127.0.0.1:$FAKE_PORT" ;;
                  exec)
                    echo exec >> "$FAKE_STATE/execs"
                    [ "$(wc -l < "$FAKE_STATE/execs")" -ge 2 ]
                    ;;
                  logs) echo 'container log secret'; exit 1 ;;
                  *) exit 1 ;;
                esac
                """);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        public void Add(string name, char id, string owner) =>
            File.WriteAllText(Path.Combine(State, name), new string(id, 64) + " " + owner + "\n");

        public (int ExitCode, string Output, string Error) Run(string command, string? owner = null, string? envFile = null, int port = 0)
        {
            var start = new ProcessStartInfo("bash") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(ProjectGraphChecker.FindRepoRoot(), "scripts", "test-services.sh"));
            start.ArgumentList.Add(command);
            start.Environment["PATH"] = root + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            start.Environment["FAKE_STATE"] = State;
            start.Environment["FAKE_PORT"] = port.ToString(CultureInfo.InvariantCulture);
            start.Environment["AUTONOGRAMMER_TEST_SERVICES_NAME"] = "lane";
            start.Environment.Remove("AUTONOGRAMMER_TEST_SERVICES_OWNER");
            start.Environment.Remove("TEST_SERVICES_ENV");
            if (owner is not null) start.Environment["AUTONOGRAMMER_TEST_SERVICES_OWNER"] = owner;
            if (envFile is not null) start.Environment["TEST_SERVICES_ENV"] = envFile;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output, error);
        }

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
