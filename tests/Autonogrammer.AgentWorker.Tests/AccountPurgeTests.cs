using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.AgentWorker.Bridge;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>Exercises <c>DELETE /accounts/{ownerDir}</c> against a real worker on a loopback port.</summary>
public sealed class AccountPurgeTests
{
    private const string SessionId = "session-1";
    private static readonly string AliceDir = Hash("alice");
    private static readonly string BobDir = Hash("bob");

    /// <summary>A session token of the worker is never a purge token: it is refused with 401 and deletes nothing.</summary>
    [Fact]
    public async Task SessionTokenIsRefusedWith401()
    {
        using var timeout = TestTimeouts.Create();
        var worker = await PurgeWorker.StartAsync(timeout.Token).ConfigureAwait(true);
        await using var workerScope = worker.ConfigureAwait(true);
        worker.CreateAccount(AliceDir);
        var token = worker.Mint(AliceDir, LegacyBrandNames.WorkerAudience, purpose: null, subject: SessionId);

        var status = await worker.DeleteAsync(AliceDir, token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Unauthorized);
        worker.AccountExists(AliceDir).ShouldBeTrue();
    }

    /// <summary>A purge token issued for another owner is refused with 403 and deletes nothing.</summary>
    [Fact]
    public async Task PurgeTokenForAnotherOwnerIsRefusedWith403()
    {
        using var timeout = TestTimeouts.Create();
        var worker = await PurgeWorker.StartAsync(timeout.Token).ConfigureAwait(true);
        await using var workerScope = worker.ConfigureAwait(true);
        worker.CreateAccount(AliceDir);
        var token = worker.Mint(BobDir);

        var status = await worker.DeleteAsync(AliceDir, token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.Forbidden);
        worker.AccountExists(AliceDir).ShouldBeTrue();
    }

    /// <summary>A malformed owner directory is refused with 400 before the token is read.</summary>
    [Fact]
    public async Task MalformedOwnerDirectoryIsRefusedWith400()
    {
        using var timeout = TestTimeouts.Create();
        var worker = await PurgeWorker.StartAsync(timeout.Token).ConfigureAwait(true);
        await using var workerScope = worker.ConfigureAwait(true);
        var token = worker.Mint(AliceDir);

        var status = await worker.DeleteAsync("ALICE", token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A purge of an account with no directory answers 404.</summary>
    [Fact]
    public async Task MissingTargetIsAnsweredWith404()
    {
        using var timeout = TestTimeouts.Create();
        var worker = await PurgeWorker.StartAsync(timeout.Token).ConfigureAwait(true);
        await using var workerScope = worker.ConfigureAwait(true);
        var token = worker.Mint(AliceDir);

        var status = await worker.DeleteAsync(AliceDir, token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A valid purge removes the owner's folder, its sessions and their files, and nothing else.</summary>
    [Fact]
    public async Task ValidPurgeDeletesOnlyTheOwnerFolder()
    {
        using var timeout = TestTimeouts.Create();
        var worker = await PurgeWorker.StartAsync(timeout.Token).ConfigureAwait(true);
        await using var workerScope = worker.ConfigureAwait(true);
        worker.CreateAccount(AliceDir);
        worker.CreateAccount(BobDir);
        var token = worker.Mint(AliceDir);

        var status = await worker.DeleteAsync(AliceDir, token, timeout.Token).ConfigureAwait(true);

        status.ShouldBe(HttpStatusCode.NoContent);
        worker.AccountExists(AliceDir).ShouldBeFalse();
        worker.AccountExists(BobDir).ShouldBeTrue();
        worker.RootExists.ShouldBeTrue();
    }

    /// <summary>A session of the account that is attached blocks the purge with 409; once it is released the purge goes through.</summary>
    [Fact]
    public async Task AttachedSessionBlocksThePurgeWith409()
    {
        using var timeout = TestTimeouts.Create();
        var worker = await PurgeWorker.StartAsync(timeout.Token).ConfigureAwait(true);
        await using var workerScope = worker.ConfigureAwait(true);
        worker.CreateAccount(AliceDir);
        var token = worker.Mint(AliceDir);
        worker.Attach(SessionId, AliceDir);

        var blocked = await worker.DeleteAsync(AliceDir, token, timeout.Token).ConfigureAwait(true);

        blocked.ShouldBe(HttpStatusCode.Conflict);
        worker.AccountExists(AliceDir).ShouldBeTrue();

        worker.Release(SessionId);
        var purged = await worker.DeleteAsync(AliceDir, worker.Mint(AliceDir), timeout.Token).ConfigureAwait(true);

        purged.ShouldBe(HttpStatusCode.NoContent);
        worker.AccountExists(AliceDir).ShouldBeFalse();
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// A worker on a loopback port with its own ES256 key pair, a fresh workspace root and a <see cref="FixedTimeProvider"/>. It plays
    /// the control plane too: <see cref="Mint"/> signs purge and session tokens with the private half of the key the worker trusts.
    /// </summary>
    private sealed class PurgeWorker : IAsyncDisposable
    {
        private readonly ECDsa _key;
        private readonly WebApplication _app;
        private readonly string _directory;
        private readonly FixedTimeProvider _clock;
        private readonly HttpClient _http = new();
        private readonly Uri _address;

        private PurgeWorker(ECDsa key, WebApplication app, string directory, FixedTimeProvider clock)
        {
            _key = key;
            _app = app;
            _directory = directory;
            _clock = clock;
            _address = new Uri(app.Urls.First(), UriKind.Absolute);
        }

        /// <summary>Gets whether the workspace root exists.</summary>
        public bool RootExists => Directory.Exists(Root);

        private string Root => Path.Combine(_directory, "work");

        /// <summary>Starts a worker that trusts a fresh key.</summary>
        /// <param name="cancellationToken">Cancels the start.</param>
        /// <returns>The running worker.</returns>
        public static async Task<PurgeWorker> StartAsync(CancellationToken cancellationToken)
        {
            var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var directory = Directory.CreateTempSubdirectory("autonogrammer-purge-").FullName;
            var clock = new FixedTimeProvider();
            var configuration = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Worker:WorkspaceRoot"] = Path.Combine(directory, "work"),
                ["Worker:Omp:Path"] = "omp",
                ["Worker:Auth:PublicKeysPem:0"] = key.ExportSubjectPublicKeyInfoPem(),
                ["Worker:CloseTimeout"] = "00:00:05",
            };
            var app = WorkerApp.Build(
                [],
                builder =>
                {
                    builder.WebHost.UseUrls("http://127.0.0.1:0");
                    builder.Configuration.AddInMemoryCollection(configuration);
                    builder.Services.AddSingleton<TimeProvider>(clock);
                });
            try
            {
                await app.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await app.DisposeAsync().ConfigureAwait(false);
                key.Dispose();
                Directory.Delete(directory, recursive: true);
                throw;
            }

            return new PurgeWorker(key, app, directory, clock);
        }

        /// <summary>Creates an account directory with one session holding a file.</summary>
        /// <param name="ownerDir">The owner directory name.</param>
        public void CreateAccount(string ownerDir)
        {
            var session = Path.Combine(Root, ownerDir, SessionId);
            Directory.CreateDirectory(session);
            File.WriteAllText(Path.Combine(session, "notes.txt"), "kept until purged");
        }

        /// <summary>Returns whether the account directory exists.</summary>
        /// <param name="ownerDir">The owner directory name.</param>
        public bool AccountExists(string ownerDir) => Directory.Exists(Path.Combine(Root, ownerDir));

        /// <summary>Attaches a session as a live connection would, without starting omp.</summary>
        /// <param name="sessionId">The session subject.</param>
        /// <param name="ownerDir">The owner directory name.</param>
        public void Attach(string sessionId, string ownerDir) =>
            _app.Services.GetRequiredService<OmpProcessHost>().TryAttach(sessionId, ownerDir, "bearer-token").ShouldNotBeNull();

        /// <summary>Releases a session attached by <see cref="Attach"/>.</summary>
        /// <param name="sessionId">The session subject.</param>
        public void Release(string sessionId) => _app.Services.GetRequiredService<OmpProcessHost>().Release(sessionId);

        /// <summary>Mints a token signed with the worker's trusted key. A purge token by default; the arguments break one rule at a time.</summary>
        /// <param name="ownerDir">The <c>owner_dir</c> claim.</param>
        /// <param name="audience">The <c>aud</c> claim.</param>
        /// <param name="purpose">The <c>purpose</c> claim; null omits it.</param>
        /// <param name="subject">The <c>sub</c> claim; null omits it.</param>
        /// <returns>The compact serialized token.</returns>
        public string Mint(string ownerDir, string audience = AccountPurgeEndpoint.Audience, string? purpose = AccountPurgeEndpoint.PurposeValue, string? subject = null)
        {
            var now = _clock.GetUtcNow();
            var claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["iss"] = LegacyBrandNames.WorkerIssuer,
                ["aud"] = audience,
                ["owner_dir"] = ownerDir,
                ["iat"] = now.ToUnixTimeSeconds(),
                ["nbf"] = now.ToUnixTimeSeconds(),
                ["exp"] = (now + TimeSpan.FromMinutes(5)).ToUnixTimeSeconds(),
            };
            if (purpose is not null)
            {
                claims[AccountPurgeEndpoint.PurposeClaimType] = purpose;
            }

            if (subject is not null)
            {
                claims["sub"] = subject;
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Claims = claims,
                SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(_key), SecurityAlgorithms.EcdsaSha256),
            };
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        /// <summary>Sends <c>DELETE /accounts/{ownerDir}</c> with the token as the bearer credential.</summary>
        /// <param name="ownerDir">The owner directory in the path.</param>
        /// <param name="token">The bearer token.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The response status.</returns>
        public async Task<HttpStatusCode> DeleteAsync(string ownerDir, string token, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(_address, "/accounts/" + ownerDir));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.StatusCode;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _http.Dispose();
            await _app.DisposeAsync().ConfigureAwait(false);
            _key.Dispose();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
