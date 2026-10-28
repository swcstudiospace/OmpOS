using System.Text.Json;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Smokes against the real <c>omp</c> binary. They issue only free commands (no prompt turns) and skip when no
/// <c>omp</c> executable is on PATH.
/// </summary>
public sealed class RpcLiveSmokeTests
{
    /// <summary>Gets whether an <c>omp</c> executable is on PATH. Gates every live smoke.</summary>
    public static bool OmpOnPath => FindOnPath("omp") is not null;

    /// <summary>
    /// Bound for omp's <c>ready</c> frame. Measured omp startup to <c>ready</c> is about 15 CPU-seconds (14.7 to 16.2 s
    /// on the gate host at load 6 to 14, 16 to 19 s wall). Wall time scales with the CPU share: 40 to 58 s at load 36
    /// to 43, and more than 120 s at load 56, so the former 60 s bound failed under full parallel load. 300 s leaves
    /// margin for that load while still failing a real hang.
    /// </summary>
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Spawns <c>omp --mode rpc --no-ui</c>, waits for <c>ready</c>, negotiates v2, reads <c>get_state</c>, closes
    /// stdin, and asserts a clean exit.
    /// </summary>
    [Fact(SkipUnless = nameof(OmpOnPath), Skip = "omp is not on PATH.")]
    public async Task ReadyNegotiateGetStateThenCloseStdinExitsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var process = OmpRpcProcess.Start("omp", RepoRoot());
        var session = new OmpRpcSession(process, HandshakeTimeout, TimeSpan.FromSeconds(60));
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            session.State.ShouldBe(HarnessState.Ready);
            session.AdvertisedCapabilities.ShouldContainKey("model.id");
            JsonElement state = await session.GetStateAsync(ct).ConfigureAwait(true);
            state.TryGetProperty("model", out _).ShouldBeTrue();

            await process.CloseInputAsync(ct).ConfigureAwait(true);
            int exitCode = await process.WaitForExitAsync(ct).ConfigureAwait(true);

            exitCode.ShouldBe(0);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The version probe parses the installed release.</summary>
    [Fact(SkipUnless = nameof(OmpOnPath), Skip = "omp is not on PATH.")]
    public async Task VersionProbeParsesInstalledRelease()
    {
        var ct = TestContext.Current.CancellationToken;

        OmpVersion version = await OmpVersionProbe.ProbeAsync("omp", ct).ConfigureAwait(true);

        version.Major.ShouldBeGreaterThan(0);
        version.ToString().ShouldStartWith($"{version.Major}.{version.Minor}.");
    }

    private static string? FindOnPath(string executable)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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

        return AppContext.BaseDirectory;
    }
}
