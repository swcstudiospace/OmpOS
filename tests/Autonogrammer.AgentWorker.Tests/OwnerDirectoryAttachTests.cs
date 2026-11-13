using System.Net;
using System.Runtime.Versioning;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>
/// The attach boundary for the owner directory: a token must carry an <c>owner_dir</c> claim that is exactly 64 lowercase hex characters,
/// or the worker refuses the attach with 403 before any session directory is touched.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class OwnerDirectoryAttachTests
{
    private const string Session = "session-1";

    /// <summary>A token without an <c>owner_dir</c> claim is refused with 403 even though its signature and every other claim are valid.</summary>
    [Fact]
    public async Task TokenWithoutOwnerDirectoryIsRefusedWith403()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var status = await WorkerAuthenticationTests.StatusAsync(host, tokens.Mint(Session, clock.GetUtcNow(), includeOwnerDir: false), timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>An <c>owner_dir</c> claim that is not exactly 64 lowercase hex characters is refused with 403 even though its signature is valid.</summary>
    /// <param name="scenario">The malformed value being checked.</param>
    [Theory]
    [InlineData("uppercase")]
    [InlineData("63-characters")]
    [InlineData("65-characters")]
    [InlineData("non-hex")]
    [InlineData("path-traversal")]
    [InlineData("empty")]
    public async Task MalformedOwnerDirectoryIsRefusedWith403(string scenario)
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var clock = new FixedTimeProvider();
        var host = await WorkerHost.StartAsync(tokens, clock, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var valid = WorkerTokenFactory.DefaultOwnerDir;
        var ownerDir = scenario switch
        {
            "uppercase" => valid.ToUpperInvariant(),
            "63-characters" => valid[..63],
            "65-characters" => valid + "0",
            "non-hex" => new string('g', 64),
            "path-traversal" => "../" + valid[..61],
            _ => string.Empty,
        };

        var status = await WorkerAuthenticationTests.StatusAsync(host, tokens.Mint(Session, clock.GetUtcNow(), ownerDir: ownerDir), timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
