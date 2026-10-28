using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using OmpOS.Api.Cloud.Options;

namespace OmpOS.Api.Cloud;

/// <summary>
/// The GitHub REST implementation of <see cref="IGitHostClient"/>. Each call asks the <see cref="IGitCredentialSource"/> for a credential limited to the
/// one repository and the least permission the call needs, and sends it in an <c>Authorization</c> header only. No response body is repeated in an
/// error: a failure carries a status-derived sentence written here.
/// </summary>
internal sealed class GitHubHostClient : IGitHostClient
{
    private const int MaximumFileBytes = 131_072;

    private readonly HttpClient _http;
    private readonly IGitCredentialSource _credentials;
    private readonly IOptionsMonitor<CloudOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="GitHubHostClient"/> class.</summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="credentials">The credential source.</param>
    /// <param name="options">The cloud options holding the API base.</param>
    public GitHubHostClient(HttpClient http, IGitCredentialSource credentials, IOptionsMonitor<CloudOptions> options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _credentials = credentials;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultBranchAsync(GitRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        using var response = await SendAsync(HttpMethod.Get, repository, RepositoryPath(repository), GitPermissions.Read, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw Fail(response.StatusCode, "reading the repository", alreadyExists: false);
        }

        var parsed = Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonContext.Default.RepositoryResponse);
        return string.IsNullOrWhiteSpace(parsed.DefaultBranch)
            ? throw new GitHostException(GitHostFailure.Unavailable, "GitHub did not report a default branch.")
            : parsed.DefaultBranch;
    }

    /// <inheritdoc />
    public async Task<bool> BranchExistsAsync(GitRepository repository, string branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        var path = RepositoryPath(repository) + "/git/ref/heads/" + EscapePath(branch);
        using var response = await SendAsync(HttpMethod.Get, repository, path, GitPermissions.Read, null, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            HttpStatusCode.NotFound => false,
            _ => throw Fail(response.StatusCode, "checking the branch", alreadyExists: false),
        };
    }

    /// <inheritdoc />
    public async Task<string?> ReadFileAsync(GitRepository repository, string path, string? reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = RepositoryPath(repository) + "/contents/" + EscapePath(path) + (reference is null ? string.Empty : "?ref=" + Uri.EscapeDataString(reference));
        using var response = await SendAsync(HttpMethod.Get, repository, target, GitPermissions.Read, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw Fail(response.StatusCode, "reading a file", alreadyExists: false);
        }

        GitHubWire.ContentResponse? content;
        try
        {
            content = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonContext.Default.ContentResponse);
        }
        catch (JsonException)
        {
            // A directory listing is a JSON array and cannot be read as one file.
            return null;
        }

        if (content is not { Type: "file", Encoding: "base64", Content: { } encoded })
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(encoded.Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
            return bytes.Length > MaximumFileBytes ? null : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<GitPullRequestResult> CreatePullRequestAsync(GitRepository repository, GitPullRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(request);
        var body = new GitHubWire.CreatePullRequestBody(request.Title, request.Head, request.Base, request.Body, request.Draft);
        using var content = new StringContent(JsonSerializer.Serialize(body, GitHubJsonContext.Default.CreatePullRequestBody), Encoding.UTF8, "application/json");
        using var response = await SendAsync(HttpMethod.Post, repository, RepositoryPath(repository) + "/pulls", GitPermissions.OpenPullRequest, content, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw Fail(response.StatusCode, "opening the pull request", await IsAlreadyExistsAsync(response, cancellationToken).ConfigureAwait(false));
        }

        var parsed = Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), GitHubJsonContext.Default.PullRequestResponse);
        return parsed.Number <= 0 || !Uri.TryCreate(parsed.HtmlUrl, UriKind.Absolute, out var htmlUrl)
            ? throw new GitHostException(GitHostFailure.Unavailable, "GitHub answered the pull request with something unreadable.")
            : new GitPullRequestResult(parsed.Number, htmlUrl, parsed.Draft);
    }

    private static string RepositoryPath(GitRepository repository) =>
        "/repos/" + Uri.EscapeDataString(repository.Owner) + "/" + Uri.EscapeDataString(repository.Name);

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        GitRepository repository,
        string pathAndQuery,
        GitPermissions permissions,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var credential = await _credentials.GetAsync([repository], permissions, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(method, new Uri(_options.CurrentValue.Git.ApiBase.TrimEnd('/') + pathAndQuery)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("OmpOS-CloudControlPlane", "1"));
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

    private static T Parse<T>(string text, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(text, typeInfo)
                ?? throw new GitHostException(GitHostFailure.Unavailable, "GitHub answered with an empty body.");
        }
        catch (JsonException ex)
        {
            throw new GitHostException(GitHostFailure.Unavailable, "GitHub answered with something unreadable.", ex);
        }
    }

    private static async Task<bool> IsAlreadyExistsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return text.Contains("A pull request already exists", StringComparison.Ordinal);
    }

    private static GitHostException Fail(HttpStatusCode status, string step, bool alreadyExists) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            new GitHostException(GitHostFailure.Unauthorized, "GitHub refused the credential while " + step + "."),
        HttpStatusCode.NotFound =>
            new GitHostException(GitHostFailure.NotFound, "GitHub did not find the repository or branch while " + step + "."),
        HttpStatusCode.UnprocessableEntity when alreadyExists =>
            new GitHostException(GitHostFailure.AlreadyExists, "A pull request for the branch already exists."),
        HttpStatusCode.UnprocessableEntity =>
            new GitHostException(GitHostFailure.Rejected, "GitHub refused the request while " + step + "."),
        _ => new GitHostException(GitHostFailure.Unavailable, "GitHub answered with an unexpected status while " + step + "."),
    };
}
