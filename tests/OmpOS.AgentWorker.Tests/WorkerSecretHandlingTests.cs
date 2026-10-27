using System.Net.Http.Headers;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.AgentWorker.Bridge;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>
/// Canary tests: a secret planted in the worker's environment and the bearer token of a connection must appear in no log line and in no
/// argv, whether the connection succeeds, fails authentication or the child echoes the secret on stderr.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class WorkerSecretHandlingTests
{
    private const string Session = "session-1";
    private const string InitializeRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}";

    /// <summary>A secret in the environment that omp echoes on stderr is redacted in the log, and neither it nor the bearer token reaches argv.</summary>
    [Fact]
    public async Task EnvironmentSecretAndBearerTokenNeverReachLogsOrArgv()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var logs = new CapturingLoggerProvider();
        var variable = "OMPOS_TEST_CANARY_TOKEN_" + Guid.NewGuid().ToString("N");
        var canary = "canary-secret-" + Guid.NewGuid().ToString("N");
        var argvFile = Path.Combine(Path.GetTempPath(), "argv-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(variable, canary);
        try
        {
            var host = await WorkerHost.StartAsync(tokens, clock, ["--argv-file", argvFile, "--stderr-env", variable], logs, cancellationToken: timeout.Token).ConfigureAwait(true);
            await using var hostScope = host.ConfigureAwait(true);
            var token = tokens.Mint(Session, clock.GetUtcNow());
            using (var socket = await WorkerSocket.ConnectAsync(host.AcpUri, token, timeout.Token).ConfigureAwait(true))
            {
                await WorkerSocket.SendTextAsync(socket, InitializeRequest, timeout.Token).ConfigureAwait(true);
                await WorkerSocket.ReceiveAsync(socket, timeout.Token).ConfigureAwait(true);
            }

            await host.Services.GetRequiredService<OmpProcessHost>().WaitForReleaseAsync(Session).WaitAsync(timeout.Token).ConfigureAwait(true);
            var argv = await File.ReadAllTextAsync(argvFile, timeout.Token).ConfigureAwait(true);

            argv.ShouldNotContain(canary, Case.Sensitive);
            argv.ShouldNotContain(token, Case.Sensitive);
            logs.Lines.ShouldNotBeEmpty();
            logs.Lines.ShouldAllBe(line => !line.Contains(canary, StringComparison.Ordinal) && !line.Contains(token, StringComparison.Ordinal));
            logs.Lines.ShouldContain(line => line.Contains("stderr: env: [REDACTED]", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
            File.Delete(argvFile);
        }
    }

    /// <summary>A refused token is never written to the log, whatever the reason it was refused.</summary>
    [Fact]
    public async Task RefusedTokenIsNeverLogged()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        using var foreign = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var logs = new CapturingLoggerProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, logs: logs, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var rejected = new[]
        {
            foreign.Mint(Session, clock.GetUtcNow()),
            tokens.Mint(Session, clock.GetUtcNow(), issuedAgo: TimeSpan.FromHours(1)),
            tokens.Mint(Session, clock.GetUtcNow(), audience: "ompos-api"),
            tokens.Mint("../escape", clock.GetUtcNow()),
        };
        using var client = new HttpClient { BaseAddress = host.Address };

        foreach (var token in rejected)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/acp");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(true);
        }

        foreach (var token in rejected)
        {
            var signature = token[(token.LastIndexOf('.') + 1)..];
            logs.Lines.ShouldAllBe(line => !line.Contains(token, StringComparison.Ordinal) && !line.Contains(signature, StringComparison.Ordinal));
        }
    }
}
