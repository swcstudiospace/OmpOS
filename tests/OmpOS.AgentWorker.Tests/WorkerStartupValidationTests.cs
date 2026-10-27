using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>The worker fails fast: a missing or unusable key and every out-of-range option stop startup with a message that names the key and never a value.</summary>
[SupportedOSPlatform("linux")]
public sealed class WorkerStartupValidationTests
{
    /// <summary>Without any public key the worker refuses to start.</summary>
    [Fact]
    public async Task MissingPublicKeyStopsStartup()
    {
        var failure = await StartExpectingFailureAsync(new Dictionary<string, string?>(StringComparer.Ordinal)).ConfigureAwait(true);

        failure.Message.ShouldContain("Worker:Auth:PublicKeysPem", Case.Sensitive);
        failure.Message.ShouldContain("At least one public key is required", Case.Sensitive);
    }

    /// <summary>A private key is refused, and the message does not repeat any of its text.</summary>
    [Fact]
    public async Task PrivateKeyIsRefusedWithoutEchoingIt()
    {
        using var tokens = new WorkerTokenFactory();
        var privatePem = tokens.PrivateKeyPem;

        var failure = await StartExpectingFailureAsync(KeyConfig(privatePem)).ConfigureAwait(true);

        failure.Message.ShouldContain("private key", Case.Sensitive);
        var body = privatePem.Split('\n')[1];
        failure.Message.ShouldNotContain(body, Case.Sensitive);
    }

    /// <summary>An RSA key under 2048 bits is refused.</summary>
    [Fact]
    public async Task WeakRsaKeyIsRefused()
    {
        using var rsa = RSA.Create(1024);

        var failure = await StartExpectingFailureAsync(KeyConfig(rsa.ExportSubjectPublicKeyInfoPem())).ConfigureAwait(true);

        failure.Message.ShouldContain("under 2048 bits", Case.Sensitive);
    }

    /// <summary>Text that is not a PEM public key is refused.</summary>
    [Fact]
    public async Task GarbageKeyIsRefused()
    {
        var failure = await StartExpectingFailureAsync(KeyConfig("this is not a key")).ConfigureAwait(true);

        failure.Message.ShouldContain("is not a PEM public key", Case.Sensitive);
    }

    /// <summary>Each out-of-range option names its configuration key in the failure.</summary>
    /// <param name="key">The configuration key set to a bad value.</param>
    /// <param name="value">The bad value.</param>
    /// <param name="expected">The text the failure must contain.</param>
    [Theory]
    [InlineData("Worker:Auth:MaxTokenLifetime", "00:20:00", "Worker:Auth:MaxTokenLifetime")]
    [InlineData("Worker:Auth:MaxTokenLifetime", "00:00:00", "Worker:Auth:MaxTokenLifetime")]
    [InlineData("Worker:Auth:ClockSkew", "00:10:00", "Worker:Auth:ClockSkew")]
    [InlineData("Worker:Auth:Issuer", " ", "Worker:Auth:Issuer")]
    [InlineData("Worker:WorkspaceRoot", "relative/work", "Worker:WorkspaceRoot")]
    [InlineData("Worker:PingInterval", "00:00:00", "Worker:PingInterval")]
    [InlineData("Worker:PingInterval", "01:00:00", "Worker:PingInterval")]
    [InlineData("Worker:CloseTimeout", "00:00:00", "Worker:CloseTimeout")]
    [InlineData("Worker:SessionId", "../escape", "Worker:SessionId")]
    [InlineData("Worker:Omp:Path", " ", "Worker:Omp:Path")]
    public async Task OutOfRangeOptionStopsStartup(string key, string value, string expected)
    {
        using var tokens = new WorkerTokenFactory();
        var config = KeyConfig(tokens.PublicKeyPem);
        config[key] = value;

        var failure = await StartExpectingFailureAsync(config).ConfigureAwait(true);

        failure.Message.ShouldContain(expected, Case.Sensitive);
    }

    /// <summary>A key given on one line with literal backslash-n sequences (as some platforms store multi-line variables) is read as a normal PEM.</summary>
    [Fact]
    public async Task SingleLineKeyWithEscapedNewlinesIsAccepted()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var oneLine = tokens.PublicKeyPem.Trim().Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Worker:Auth:PublicKeysPem:0"] = oneLine };
        var host = await WorkerHost.StartAsync(tokens, new FixedTimeProvider(), settings: settings, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        host.Address.Port.ShouldBeGreaterThan(0);
    }

    private static Dictionary<string, string?> KeyConfig(string pem) =>
        new(StringComparer.Ordinal) { ["Worker:Auth:PublicKeysPem:0"] = pem };

    private static async Task<OptionsValidationException> StartExpectingFailureAsync(Dictionary<string, string?> configuration)
    {
        using var timeout = TestTimeouts.Create();
        var app = WorkerApp.Build(
            [],
            builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(configuration);
            });
        await using var appScope = app.ConfigureAwait(true);

        return await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync(timeout.Token)).ConfigureAwait(true);
    }
}
