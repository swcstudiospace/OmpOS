using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Projects;
using Autonogrammer.Cowork.Instructions;

namespace Autonogrammer.Cowork.Projects;

/// <summary>
/// HTTP client for project metadata and global instructions.
/// The bearer comes from the token supplier on each call and is never stored.
/// A body that names an account is refused before send. Folders are not a contract member.
/// </summary>
public sealed class HttpProjectApi : IProjectApi
{
    /// <summary>The projects route.</summary>
    public const string ProjectsPath = "/cowork/projects";

    /// <summary>The global instructions route.</summary>
    public const string GlobalInstructionsPath = "/cowork/instructions/global";

    private readonly HttpClient _http;
    private readonly Func<string?> _tokenSupplier;

    /// <summary>Initializes a new instance of the <see cref="HttpProjectApi"/> class.</summary>
    /// <param name="http">The client. The base address is the account API.</param>
    /// <param name="tokenSupplier">Returns the current bearer. Called on each send. Never stored.</param>
    public HttpProjectApi(HttpClient http, Func<string?> tokenSupplier)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(tokenSupplier);
        _http = http;
        _tokenSupplier = tokenSupplier;
    }

    /// <inheritdoc />
    public Task<ProjectApiResult<IReadOnlyList<ProjectSyncRecord>>> ListAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ProjectSyncRecord>>(HttpMethod.Get, ProjectsPath, content: null, cancellationToken);

    /// <inheritdoc />
    public Task<ProjectApiResult<ProjectSyncRecord>> UpsertAsync(ProjectSyncRecord project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var json = JsonSerializer.Serialize(project, CoworkProjectJsonContext.Default.ProjectSyncRecord);
        return SendAsync<ProjectSyncRecord>(HttpMethod.Put, ProjectsPath + "/" + Uri.EscapeDataString(project.Id), json, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProjectApiResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return SendAsync<bool>(HttpMethod.Post, ProjectsPath + "/" + Uri.EscapeDataString(id) + "/archive", content: "{}", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProjectApiResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return SendAsync<bool>(HttpMethod.Delete, ProjectsPath + "/" + Uri.EscapeDataString(id), content: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProjectApiResult<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken)
    {
        var result = await SendAsync<GlobalInstructionsRecord>(HttpMethod.Get, GlobalInstructionsPath, content: null, cancellationToken)
            .ConfigureAwait(false);
        return new ProjectApiResult<string>(result.Succeeded, result.StatusCode, result.Offline, result.Value?.Instructions, result.Reason, result.State);
    }

    /// <inheritdoc />
    public Task<ProjectApiResult<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        if (Encoding.UTF8.GetByteCount(instructions) > InstructionLimits.GlobalInstructionsBytes)
        {
            return Task.FromResult(new ProjectApiResult<bool>(false, null, false, false, Instructions.StandingContextBuilder.InstructionsTruncated, ProjectSyncState.Synced));
        }

        var json = JsonSerializer.Serialize(new GlobalInstructionsRecord { Instructions = instructions }, CoworkProjectJsonContext.Default.GlobalInstructionsRecord);
        return SendAsync<bool>(HttpMethod.Put, GlobalInstructionsPath, json, cancellationToken);
    }

    /// <summary>Returns whether a JSON body names an account or owner. Such a body is refused before send.</summary>
    /// <param name="json">The body.</param>
    /// <returns>True when the body names an account.</returns>
    public static bool BodyNamesAccount(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return NamesAccount(document.RootElement);
        }
        catch (JsonException)
        {
            return json.Contains("\"account\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"accountId\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"owner\"", StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<ProjectApiResult<T>> SendAsync<T>(HttpMethod method, string path, string? content, CancellationToken cancellationToken)
    {
        if (content is not null && BodyNamesAccount(content))
        {
            return new ProjectApiResult<T>(false, (int)HttpStatusCode.BadRequest, false, default, "A body naming an account is refused.", ProjectSyncState.SignedOut);
        }

        var token = _tokenSupplier();
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ProjectApiResult<T>(false, (int)HttpStatusCode.Unauthorized, false, default, ProjectSyncState.SignedOut, ProjectSyncState.SignedOut);
        }

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
        {
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ProjectApiResult<T>(false, 401, false, default, ProjectSyncState.TokenExpired, ProjectSyncState.TokenExpired);
            }

            if ((int)response.StatusCode == 429)
            {
                return new ProjectApiResult<T>(false, 429, false, default, ProjectSyncState.RateLimited, ProjectSyncState.RateLimited);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ProjectApiResult<T>(false, (int)response.StatusCode, false, default, body, ProjectSyncState.Offline);
            }

            var value = Decode<T>(body);
            return new ProjectApiResult<T>(true, (int)response.StatusCode, false, value, null, ProjectSyncState.Synced);
        }
        catch (HttpRequestException exception)
        {
            return new ProjectApiResult<T>(false, null, true, default, exception.Message, ProjectSyncState.Offline);
        }
    }

    private static T? Decode<T>(string body)
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)true;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        if (typeof(T) == typeof(ProjectSyncRecord))
        {
            return (T?)(object?)JsonSerializer.Deserialize(body, CoworkProjectJsonContext.Default.ProjectSyncRecord);
        }

        if (typeof(T) == typeof(IReadOnlyList<ProjectSyncRecord>))
        {
            var list = JsonSerializer.Deserialize(body, CoworkProjectJsonContext.Default.ListProjectSyncRecord);
            return (T?)(object?)list;
        }

        if (typeof(T) == typeof(GlobalInstructionsRecord))
        {
            return (T?)(object?)JsonSerializer.Deserialize(body, CoworkProjectJsonContext.Default.GlobalInstructionsRecord);
        }

        return default;
    }

    private static bool NamesAccount(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("account") || property.NameEquals("accountId") || property.NameEquals("owner"))
                {
                    return true;
                }

                if (NamesAccount(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (NamesAccount(item))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
