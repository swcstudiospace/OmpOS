using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// Behavioural tests of scripts/vercel/render-csp.sh, the build-time generator of the CSP connect-src value
/// that vercel.ts consumes (Q-VERCEL-CSP). Each test runs the script as a process with a controlled environment.
/// </summary>
public sealed class RenderCspScriptTests
{
    private const string OriginVariable = "AUTONOGRAMMER_API_ORIGIN";
    private const string LoopbackVariable = "AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK";
    private const string DemoVariable = "AUTONOGRAMMER_DEMO";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A valid https origin, with or without an explicit port, prints the exact connect-src line.</summary>
    [Theory]
    [InlineData("https://api.autonogrammer.example", "connect-src 'self' https://api.autonogrammer.example\n")]
    [InlineData("https://api.autonogrammer.example:8443", "connect-src 'self' https://api.autonogrammer.example:8443\n")]
    [InlineData("https://a-b.c.example:443", "connect-src 'self' https://a-b.c.example:443\n")]
    public async Task ValidHttpsOriginPrintsExactConnectSrcLine(string origin, string expectedStdout)
    {
        var result = await RunScriptAsync(origin, allowLoopback: null);

        result.ExitCode.ShouldBe(0, $"render-csp.sh must accept a valid https origin. STDERR:\n{result.StandardError}");
        result.StandardOutput.ShouldBe(expectedStdout);
        result.StandardError.ShouldBe(string.Empty);
    }

    /// <summary>Loopback http is accepted only for local development, when the explicit flag is set.</summary>
    [Fact]
    public async Task LoopbackOriginWithAllowFlagPrintsConnectSrcLine()
    {
        var result = await RunScriptAsync("http://127.0.0.1:5187", allowLoopback: "1");

        result.ExitCode.ShouldBe(0, $"render-csp.sh must accept loopback when the flag is 1. STDERR:\n{result.StandardError}");
        result.StandardOutput.ShouldBe("connect-src 'self' http://127.0.0.1:5187\n");
    }

    /// <summary>Loopback http without the explicit flag is rejected, and the origin is not echoed.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public async Task LoopbackOriginWithoutAllowFlagIsRejectedWithoutEchoingOrigin(string? allowLoopback)
    {
        const string origin = "http://127.0.0.1:5187";

        var result = await RunScriptAsync(origin, allowLoopback);

        AssertRejected(result, origin);
    }

    /// <summary>Wildcard hosts are never accepted, even with the loopback flag set.</summary>
    [Fact]
    public async Task WildcardOriginIsRejectedWithoutEchoingOrigin()
    {
        const string origin = "https://*.autonogrammer.example";

        var result = await RunScriptAsync(origin, allowLoopback: "1");

        AssertRejected(result, origin);
    }

    /// <summary>A trailing slash is rejected, because the CSP source must be an origin with no path.</summary>
    [Fact]
    public async Task TrailingSlashOriginIsRejectedWithoutEchoingOrigin()
    {
        const string origin = "https://api.autonogrammer.example/";

        var result = await RunScriptAsync(origin, allowLoopback: null);

        AssertRejected(result, origin);
    }

    /// <summary>A path is rejected, so only the origin reaches the CSP value.</summary>
    [Fact]
    public async Task PathOriginIsRejectedWithoutEchoingOrigin()
    {
        const string origin = "https://api.autonogrammer.example/v1";

        var result = await RunScriptAsync(origin, allowLoopback: null);

        AssertRejected(result, origin);
    }

    /// <summary>A missing value is rejected with a message that names the variable, not a value.</summary>
    [Fact]
    public async Task MissingOriginIsRejected()
    {
        var result = await RunScriptAsync(origin: null, allowLoopback: null);

        result.ExitCode.ShouldNotBe(0, "render-csp.sh must fail when AUTONOGRAMMER_API_ORIGIN is unset.");
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldContain(OriginVariable);
    }

    /// <summary>Query, fragment, port 0, and plain http on a public host are rejected without echoing the origin.</summary>
    [Theory]
    [InlineData("https://api.autonogrammer.example?debug=1")]
    [InlineData("https://api.autonogrammer.example#fragment")]
    [InlineData("https://api.autonogrammer.example:0")]
    [InlineData("https://api.autonogrammer.example:65536")]
    [InlineData("http://api.autonogrammer.example")]
    public async Task OtherInvalidOriginsAreRejectedWithoutEchoingOrigin(string origin)
    {
        var result = await RunScriptAsync(origin, allowLoopback: null);

        AssertRejected(result, origin);
    }

    /// <summary>Userinfo can carry credentials, so the rejection must not echo any part of the value.</summary>
    [Fact]
    public async Task UserinfoOriginIsRejectedWithoutEchoingCredentials()
    {
        const string origin = "https://deploy-user:s3cret-token@api.autonogrammer.example";

        var result = await RunScriptAsync(origin, allowLoopback: null);

        AssertRejected(result, origin);
        result.StandardError.ShouldNotContain("s3cret-token");
        result.StandardError.ShouldNotContain("deploy-user");
    }

    /// <summary>The Demo build needs no API origin and allows only the page's own origin.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task DemoBuildEmitsSelfOnlyWithoutAnOrigin()
    {
        var result = await RunScriptAsync(origin: null, allowLoopback: null, demo: "1");

        result.ExitCode.ShouldBe(0, $"render-csp.sh must accept a Demo build without an origin. STDERR:\n{result.StandardError}");
        result.StandardOutput.ShouldBe("connect-src 'self'\n");
        result.StandardError.ShouldBe(string.Empty);
    }

    /// <summary>The Demo ignores an origin: it never reaches the policy, even a hostile one.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task DemoBuildIgnoresAnyOrigin()
    {
        var result = await RunScriptAsync("https://*.evil.example", allowLoopback: null, demo: "1");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("connect-src 'self'\n");
    }

    /// <summary>Only the literal 1 selects the Demo: any other value is a normal build and still fails closed without an origin.</summary>
    /// <param name="demo">The value of AUTONOGRAMMER_DEMO.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("true")]
    [InlineData("yes")]
    public async Task OnlyTheLiteralOneSelectsTheDemo(string demo)
    {
        var result = await RunScriptAsync(origin: null, allowLoopback: null, demo: demo);

        result.ExitCode.ShouldNotBe(0, "a normal build must still fail closed without AUTONOGRAMMER_API_ORIGIN.");
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldContain(OriginVariable);
    }

    /// <summary>A normal build with an origin is unchanged when AUTONOGRAMMER_DEMO is unset.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task NormalBuildStillPrintsItsOrigin()
    {
        var result = await RunScriptAsync("https://api.autonogrammer.example", allowLoopback: null, demo: null);

        result.StandardOutput.ShouldBe("connect-src 'self' https://api.autonogrammer.example\n");
    }

    /// <summary>A valid auth origin is a second connect-src origin. The value is printed only on success.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AValidAuthOriginIsASecondConnectSrcOrigin()
    {
        var result = await RunScriptAsync(
            "https://api.autonogrammer.example",
            allowLoopback: null,
            authOrigin: "https://auth.autonogrammer.example");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("connect-src 'self' https://api.autonogrammer.example https://auth.autonogrammer.example\n");
    }

    /// <summary>Sign-in enabled (only the literal 1) with a missing auth origin fails closed.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SignInEnabledWithoutAnAuthOriginFailsClosed()
    {
        var result = await RunScriptAsync("https://api.autonogrammer.example", allowLoopback: null, signIn: "1");

        result.ExitCode.ShouldNotBe(0);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldContain("AUTONOGRAMMER_AUTH_ORIGIN");
    }

    /// <summary>An http or wildcard auth origin is rejected and never echoed.</summary>
    /// <param name="authOrigin">The rejected origin.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("http://auth.autonogrammer.example")]
    [InlineData("https://*.autonogrammer.example")]
    public async Task AnInvalidAuthOriginIsRejectedWithoutEchoingIt(string authOrigin)
    {
        var result = await RunScriptAsync("https://api.autonogrammer.example", allowLoopback: null, authOrigin: authOrigin);

        AssertRejected(result, authOrigin);
    }

    /// <summary>The Demo build keeps connect-src self whatever the auth variables say.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TheDemoIgnoresTheAuthOrigin()
    {
        var result = await RunScriptAsync(
            "https://api.autonogrammer.example",
            allowLoopback: null,
            demo: "1",
            authOrigin: "https://auth.autonogrammer.example",
            signIn: "1");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("connect-src 'self'\n");
    }


    /// <summary>The frame-src mode prints the render origin as the only frame source, with the same validation as connect-src.</summary>
    [Fact]
    public async Task FrameSrcModePrintsTheRenderOriginOnly()
    {
        var result = await RunScriptAsync("https://api.autonogrammer.example", allowLoopback: null, authOrigin: "https://auth.autonogrammer.example", argument: "--frame-src");

        result.ExitCode.ShouldBe(0, $"render-csp.sh --frame-src must accept a valid origin. STDERR:\n{result.StandardError}");
        result.StandardOutput.ShouldBe("frame-src 'self' https://api.autonogrammer.example\n", "the auth origin is never a frame source");
    }

    /// <summary>The Demo has no frame request, so the frame-src mode prints nothing for it, whatever the other variables say.</summary>
    [Fact]
    public async Task FrameSrcModePrintsNothingForTheDemo()
    {
        var result = await RunScriptAsync("https://api.autonogrammer.example", allowLoopback: null, demo: "1", argument: "--frame-src");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(string.Empty);
    }

    /// <summary>An invalid origin prints no frame source and never echoes the value, as the connect-src mode does.</summary>
    [Theory]
    [InlineData("https://*.autonogrammer.example")]
    [InlineData("https://api.autonogrammer.example/")]
    [InlineData("http://127.0.0.1:5187")]
    public async Task FrameSrcModeRejectsAnInvalidOriginWithoutEchoingIt(string origin)
    {
        var result = await RunScriptAsync(origin, allowLoopback: null, argument: "--frame-src");

        AssertRejected(result, origin);
    }

    /// <summary>The loopback origin is a frame source only with the same explicit local-development flag.</summary>
    [Fact]
    public async Task FrameSrcModeAcceptsLoopbackWithTheFlag()
    {
        var result = await RunScriptAsync("http://127.0.0.1:5187", allowLoopback: "1", argument: "--frame-src");

        result.ExitCode.ShouldBe(0, $"STDERR:\n{result.StandardError}");
        result.StandardOutput.ShouldBe("frame-src 'self' http://127.0.0.1:5187\n");
    }

    /// <summary>Any argument other than --frame-src is refused.</summary>
    [Fact]
    public async Task AnUnknownArgumentIsRefused()
    {
        var result = await RunScriptAsync("https://api.autonogrammer.example", allowLoopback: null, argument: "--connect-src");

        result.ExitCode.ShouldNotBe(0);
        result.StandardOutput.ShouldBe(string.Empty);
    }

    private static void AssertRejected(ScriptResult result, string origin)
    {
        result.ExitCode.ShouldNotBe(0, "render-csp.sh must reject an origin outside the allowed form.");
        result.StandardOutput.ShouldBe(string.Empty, "A rejected origin must print no connect-src value.");
        result.StandardError.Contains(origin, StringComparison.Ordinal).ShouldBeFalse("The rejection message must not echo the origin value.");
    }

    private static async Task<ScriptResult> RunScriptAsync(string? origin, string? allowLoopback, string? demo = null, string? authOrigin = null, string? signIn = null, string? argument = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var root = RepoRoot();
        var script = Path.Combine(root.FullName, "scripts", "vercel", "render-csp.sh");
        File.Exists(script).ShouldBeTrue($"render-csp.sh must exist at {script}.");

        var startInfo = new ProcessStartInfo("bash")
        {
            WorkingDirectory = root.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(script);
        if (argument is not null)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Set or clear both variables so an operator's shell environment cannot change the outcome.
        SetOrRemove(startInfo.Environment, OriginVariable, origin);
        SetOrRemove(startInfo.Environment, LoopbackVariable, allowLoopback);
        SetOrRemove(startInfo.Environment, DemoVariable, demo);
        SetOrRemove(startInfo.Environment, "AUTONOGRAMMER_AUTH_ORIGIN", authOrigin);
        SetOrRemove(startInfo.Environment, "AUTONOGRAMMER_SIGNIN", signIn);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("bash did not start.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProcessTimeout);
        var exited = true;
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            exited = false;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(ct).ConfigureAwait(true);
        }

        var stdout = await stdoutTask.ConfigureAwait(true);
        var stderr = await stderrTask.ConfigureAwait(true);
        exited.ShouldBeTrue("render-csp.sh timed out.");

        return new ScriptResult(process.ExitCode, stdout, stderr);
    }

    private static void SetOrRemove(IDictionary<string, string?> environment, string name, string? value)
    {
        if (value is null)
        {
            environment.Remove(name);
        }
        else
        {
            environment[name] = value;
        }
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        return dir;
    }

    private sealed record ScriptResult(int ExitCode, string StandardOutput, string StandardError);
}
