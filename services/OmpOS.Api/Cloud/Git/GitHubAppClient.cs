using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OmpOS.Api.Cloud.Options;

namespace OmpOS.Api.Cloud;

/// <summary>
/// Mints GitHub App installation tokens. For each request it signs a short-lived App JWT (RS256), finds the installation of the repositories (or uses
/// the configured one), and asks GitHub for an installation token limited to exactly those repositories and the permissions the step needs. The
/// token is cached by <see cref="InstallationTokenCache"/> and replaced five minutes before <c>expires_at</c>. The App key and the tokens are never
/// logged, never written to a file and never put on a command line.
/// </summary>
internal sealed class GitHubAppClient : IGitCredentialSource, IDisposable
{
    private static readonly TimeSpan AppTokenLifetime = TimeSpan.FromMinutes(9);
    private static readonly TimeSpan AppTokenBackdate = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;
    private readonly InstallationTokenCache _cache;
    private readonly SemaphoreSlim _mintGate = new(1, 1);
    private RSA? _key;
    private SigningCredentials? _credentials;
    private string? _keySource;

    /// <summary>Initializes a new instance of the <see cref="GitHubAppClient"/> class.</summary>
    /// <param name="http">The HTTP client; its base address is not used.</param>
    /// <param name="options">The cloud options holding the App settings.</param>
    /// <param name="time">The clock that decides expiry.</param>
    /// <param name="cache">The token cache.</param>
    public GitHubAppClient(HttpClient http, IOptionsMonitor<CloudOptions> options, TimeProvider time, InstallationTokenCache cache)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(cache);
        _http = http;
        _options = options;
        _time = time;
        _cache = cache;
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CurrentValue.Git.App.AppId) && !string.IsNullOrWhiteSpace(_options.CurrentValue.Git.App.PrivateKeyPem);

    /// <inheritdoc />
    public async Task<GitCredential> GetAsync(IReadOnlyList<GitRepository> repositories, GitPermissions permissions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(permissions);
        if (!IsConfigured)
        {
            throw new GitHostException(GitHostFailure.NotConfigured, "No GitHub App is configured (Cloud:Git:App).");
        }

        if (repositories.Count == 0)
        {
            throw new GitHostException(GitHostFailure.Unsupported, "A token needs at least one repository.");
        }

        var key = InstallationTokenCache.KeyOf(repositories, permissions);
        if (_cache.TryGet(key, _time.GetUtcNow(), out var cached))
        {
            return cached!;
        }

        await _mintGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGet(key, _time.GetUtcNow(), out cached))
            {
                return cached!;
            }

            var minted = await MintAsync(repositories, permissions, cancellationToken).ConfigureAwait(false);
            _cache.Set(key, minted);
            return minted;
        }
        finally
        {
            _ = _mintGate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _key?.Dispose();
        _mintGate.Dispose();
    }

    private async Task<GitCredential> MintAsync(IReadOnlyList<GitRepository> repositories, GitPermissions permissions, CancellationToken cancellationToken)
    {
        var settings = _options.CurrentValue.Git;
        var apiBase = settings.ApiBase.TrimEnd('/');
        var appJwt = CreateAppJwt(settings.App);
        var installationId = settings.App.InstallationId;
        if (string.IsNullOrWhiteSpace(installationId))
        {
            installationId = await FindInstallationAsync(apiBase, repositories, appJwt, cancellationToken).ConfigureAwait(false);
        }

        var body = new GitHubWire.InstallationTokenRequest(
            [.. repositories.Select(static repository => repository.Name).Distinct(StringComparer.Ordinal)],
            permissions.ToDictionary());
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{apiBase}/app/installations/{Uri.EscapeDataString(installationId)}/access_tokens"))
        {
            Content = new StringContent(JsonSerializer.Serialize(body, GitHubJsonContext.Default.InstallationTokenRequest), System.Text.Encoding.UTF8, "application/json"),
        };
        Authorize(request, appJwt);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw Fail(response.StatusCode, "minting an installation token");
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = TryParse(text, GitHubJsonContext.Default.InstallationTokenResponse);
        return parsed is null || string.IsNullOrEmpty(parsed.Token)
            ? throw new GitHostException(GitHostFailure.Unavailable, "GitHub answered the token request with something unreadable.")
            : new GitCredential(parsed.Token, parsed.ExpiresAt);
    }

    private async Task<string> FindInstallationAsync(string apiBase, IReadOnlyList<GitRepository> repositories, string appJwt, CancellationToken cancellationToken)
    {
        string? found = null;
        foreach (var repository in repositories.DistinctBy(static repository => repository.Owner, StringComparer.Ordinal))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{apiBase}/repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}/installation"));
            Authorize(request, appJwt);
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw Fail(response.StatusCode, "finding the App installation");
            }

            var parsed = TryParse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonContext.Default.InstallationResponse)
                ?? throw new GitHostException(GitHostFailure.Unavailable, "GitHub answered the installation lookup with something unreadable.");
            var id = parsed.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (found is not null && !string.Equals(found, id, StringComparison.Ordinal))
            {
                throw new GitHostException(GitHostFailure.Unsupported, "The repositories belong to different App installations; one token cannot cover them.");
            }

            found = id;
        }

        return found ?? throw new GitHostException(GitHostFailure.Unsupported, "No installation was found for the repositories.");
    }

    private string CreateAppJwt(CloudOptions.GitAppSettings app)
    {
        var credentials = CredentialsFor(app.PrivateKeyPem);
        var now = _time.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = app.AppId,
            IssuedAt = (now - AppTokenBackdate).UtcDateTime,
            NotBefore = (now - AppTokenBackdate).UtcDateTime,
            Expires = (now + AppTokenLifetime).UtcDateTime,
            SigningCredentials = credentials,
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private SigningCredentials CredentialsFor(string pem)
    {
        if (_credentials is not null && string.Equals(_keySource, pem, StringComparison.Ordinal))
        {
            return _credentials;
        }

        _key?.Dispose();
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(CloudOptionsValidator.NormalizePem(pem));
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            key.Dispose();
            _key = null;
            _credentials = null;
            throw new GitHostException(GitHostFailure.NotConfigured, "Cloud:Git:App:PrivateKeyPem is not an RSA private key PEM.", ex);
        }

        _key = key;
        _keySource = pem;
        _credentials = new SigningCredentials(new RsaSecurityKey(key), SecurityAlgorithms.RsaSha256);
        return _credentials;
    }

    private static void Authorize(HttpRequestMessage request, string bearer)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("OmpOS-CloudControlPlane", "1"));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GitHostException(GitHostFailure.Unavailable, "GitHub could not be reached.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHostException(GitHostFailure.Unavailable, "GitHub did not answer in time.", ex);
        }
    }

    private static T? TryParse<T>(string text, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(text, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static GitHostException Fail(HttpStatusCode status, string step) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            new GitHostException(GitHostFailure.Unauthorized, "GitHub refused the App credential while " + step + "."),
        HttpStatusCode.NotFound =>
            new GitHostException(GitHostFailure.NotFound, "GitHub did not find the App installation or repository while " + step + "."),
        HttpStatusCode.UnprocessableEntity =>
            new GitHostException(GitHostFailure.Rejected, "GitHub refused the request while " + step + "."),
        _ => new GitHostException(GitHostFailure.Unavailable, "GitHub answered with an unexpected status while " + step + "."),
    };
}
