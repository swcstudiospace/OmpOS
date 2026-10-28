using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>
/// A fake GitHub REST API for the control plane's HTTP client: the App installation lookup, installation token minting (it checks the App JWT is RS256
/// and signed by the configured key), repository facts, branch existence, file contents and pull request creation. It records every request, so a test
/// can assert what was asked for (the repositories and permissions of a token, the pull request payload) and with which credential.
/// </summary>
internal sealed class FakeGitHubHandler : HttpMessageHandler
{
    private readonly SecurityKey _appKey;
    private const string AppId = "123";

    private readonly TimeProvider _clock;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private int _mints;

    /// <summary>Initializes a new instance of the <see cref="FakeGitHubHandler"/> class.</summary>
    /// <param name="appKey">The key App JWTs must verify with.</param>
    /// <param name="clock">The clock that stamps <c>expires_at</c>.</param>
    public FakeGitHubHandler(SecurityKey appKey, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(appKey);
        ArgumentNullException.ThrowIfNull(clock);
        _appKey = appKey;
        _clock = clock;
    }

    /// <summary>One request the fake saw.</summary>
    /// <param name="Method">The HTTP method.</param>
    /// <param name="Path">The absolute path.</param>
    /// <param name="Query">The query string, with its leading question mark.</param>
    /// <param name="Authorization">The <c>Authorization</c> header.</param>
    /// <param name="Body">The request body, or empty.</param>
    internal sealed record RecordedRequest(string Method, string Path, string Query, string Authorization, string Body);

    /// <summary>Gets or sets the default branch the fake reports.</summary>
    public string DefaultBranch { get; set; } = "main";

    /// <summary>Gets or sets the installation identifier the fake reports.</summary>
    public long InstallationId { get; set; } = 42;

    /// <summary>Gets or sets how long a minted token lives.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets whether branches exist on the host. By default none does, as before the agent pushed.</summary>
    public Func<string, bool> BranchExists { get; set; } = static _ => false;

    /// <summary>Gets or sets whether the next pull request creation is answered as "already exists".</summary>
    public bool PullRequestAlreadyExists { get; set; }

    /// <summary>Gets or sets the pull request number the fake assigns.</summary>
    public int PullRequestNumber { get; set; } = 7;

    /// <summary>Gets the files the fake serves, by repository path.</summary>
    public ConcurrentDictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the requests seen, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    /// <summary>Gets how many installation tokens were minted.</summary>
    public int Mints => Volatile.Read(ref _mints);

    /// <summary>Gets the token the fake issued for the given mint number (1-based).</summary>
    /// <param name="mint">The mint number.</param>
    /// <returns>The token text.</returns>
    public static string TokenOf(int mint) => "ghs_fake_installation_token_" + mint.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri!;
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var authorization = request.Headers.Authorization?.ToString() ?? string.Empty;
        _requests.Enqueue(new RecordedRequest(request.Method.Method, uri.AbsolutePath, uri.Query, authorization, body));
        var path = uri.AbsolutePath;
        var segments = path.Trim('/').Split('/');
        if (segments is ["app", "installations", _, "access_tokens"] && request.Method == HttpMethod.Post)
        {
            return await MintAsync(authorization).ConfigureAwait(false);
        }

        if (segments is ["repos", _, _, "installation"] && request.Method == HttpMethod.Get)
        {
            return await IsAppJwtAsync(authorization).ConfigureAwait(false) ? Json(HttpStatusCode.OK, "{\"id\":" + InstallationId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}") : Status(HttpStatusCode.Unauthorized);
        }

        if (segments is ["repos", _, _] && request.Method == HttpMethod.Get)
        {
            return Json(HttpStatusCode.OK, new JsonObject { ["default_branch"] = DefaultBranch }.ToJsonString());
        }

        if (segments is ["repos", _, _, "git", "ref", "heads", ..] && request.Method == HttpMethod.Get)
        {
            var branch = string.Join('/', segments.Skip(6));
            return BranchExists(branch) ? Json(HttpStatusCode.OK, "{\"ref\":\"refs/heads/" + branch + "\"}") : Status(HttpStatusCode.NotFound);
        }

        if (segments is ["repos", _, _, "contents", ..] && request.Method == HttpMethod.Get)
        {
            var file = string.Join('/', segments.Skip(4));
            return Files.TryGetValue(file, out var content)
                ? Json(HttpStatusCode.OK, new JsonObject { ["type"] = "file", ["encoding"] = "base64", ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) }.ToJsonString())
                : Status(HttpStatusCode.NotFound);
        }

        if (segments is ["repos", _, _, "pulls"] && request.Method == HttpMethod.Post)
        {
            if (PullRequestAlreadyExists)
            {
                return Json(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Validation Failed\",\"errors\":[{\"message\":\"A pull request already exists for o:branch.\"}]}");
            }

            using var document = JsonDocument.Parse(body);
            var draft = document.RootElement.GetProperty("draft").GetBoolean();
            var number = PullRequestNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Json(HttpStatusCode.Created, "{\"number\":" + number + ",\"html_url\":\"https://github.test/" + segments[1] + "/" + segments[2] + "/pull/" + number + "\",\"draft\":" + (draft ? "true" : "false") + "}");
        }

        return Status(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    private async Task<HttpResponseMessage> MintAsync(string authorization)
    {
        if (!await IsAppJwtAsync(authorization).ConfigureAwait(false))
        {
            return Status(HttpStatusCode.Unauthorized);
        }

        var mint = Interlocked.Increment(ref _mints);
        var expires = (_clock.GetUtcNow() + TokenLifetime).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        return Json(HttpStatusCode.Created, new JsonObject { ["token"] = TokenOf(mint), ["expires_at"] = expires }.ToJsonString());
    }

    private async Task<bool> IsAppJwtAsync(string authorization)
    {
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return false;
        }

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            authorization["Bearer ".Length..],
            new TokenValidationParameters
            {
                ValidIssuer = AppId,
                AudienceValidator = static (audiences, _, _) => !audiences.Any(),
                LifetimeValidator = TestTokenLifetime.Of(_clock),
                IssuerSigningKey = _appKey,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            }).ConfigureAwait(false);
        return result.IsValid;
    }
}
