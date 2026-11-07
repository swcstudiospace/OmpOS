#pragma warning disable MA0048 // The plan owns this file for the call result and the client.
using System.Net.Http.Json;
using System.Text;
using Autonogrammer.Contracts.Cowork.Projects;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Scheduling;

namespace Autonogrammer.Web.Services.Cowork;

/// <summary>One web call. The sentence matches the desktop model. Instruction text is not repeated in <see cref="Sentence"/> on success.</summary>
/// <typeparam name="T">The payload.</typeparam>
/// <param name="Succeeded">True on a 2xx response.</param>
/// <param name="Sentence">The degraded or limit sentence, or null.</param>
/// <param name="Value">The payload.</param>
internal sealed record CoworkWebCall<T>(bool Succeeded, string? Sentence, T? Value);

/// <summary>
/// Projects and global instructions over <see cref="ApiHttp"/>. The bearer is added by <c>BearerTokenHandler</c>.
/// This client does not reference the desktop cowork assembly and does not use browser storage.
/// </summary>
internal sealed class CoworkProjectsClient
{
    private readonly ApiHttp _http;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The shared API client.</param>
    internal CoworkProjectsClient(ApiHttp http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>Gets <c>/cowork/projects</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The records, or a degraded sentence.</returns>
    internal async Task<CoworkWebCall<IReadOnlyList<ProjectSyncRecord>>> ListAsync(CancellationToken cancellationToken)
    {
        var call = await SendAsync<List<ProjectSyncRecord>>(HttpMethod.Get, "/cowork/projects", content: null, cancellationToken).ConfigureAwait(false);
        return new CoworkWebCall<IReadOnlyList<ProjectSyncRecord>>(call.Succeeded, call.Sentence, call.Value);
    }

    /// <summary>Puts one project. The body is the contract record, which has no folders member.</summary>
    /// <param name="project">The metadata.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    internal Task<CoworkWebCall<ProjectSyncRecord>> UpsertAsync(ProjectSyncRecord project, CancellationToken cancellationToken) =>
        SendAsync<ProjectSyncRecord>(HttpMethod.Put, "/cowork/projects/" + Uri.EscapeDataString(project.Id), project, cancellationToken);

    /// <summary>Deletes <c>/cowork/projects/{id}</c>.</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    internal Task<CoworkWebCall<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
        SendAsync<bool>(HttpMethod.Delete, "/cowork/projects/" + Uri.EscapeDataString(id), content: null, cancellationToken);

    /// <summary>Gets <c>/cowork/instructions/global</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instructions text.</returns>
    internal async Task<CoworkWebCall<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken)
    {
        var call = await SendAsync<GlobalInstructionsRecord>(HttpMethod.Get, "/cowork/instructions/global", content: null, cancellationToken).ConfigureAwait(false);
        return new CoworkWebCall<string>(call.Succeeded, call.Sentence, call.Value?.Instructions);
    }

    /// <summary>Puts <c>/cowork/instructions/global</c>. An over-budget body is refused before send.</summary>
    /// <param name="instructions">The instructions.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    internal async Task<CoworkWebCall<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        if (Encoding.UTF8.GetByteCount(instructions) > GlobalInstructionsModel.GlobalInstructionsBytes)
        {
            return new CoworkWebCall<bool>(false, GlobalInstructionsModel.LimitMessage, false);
        }

        var call = await SendAsync<bool>(HttpMethod.Put, "/cowork/instructions/global", new GlobalInstructionsRecord { Instructions = instructions }, cancellationToken).ConfigureAwait(false);
        return call;
    }

    /// <summary>The web list wording for a desktop-only scheduled task. Nothing is started.</summary>
    /// <param name="task">The session task.</param>
    /// <returns>Next run, last run and missed runs, plus the desktop-only sentence.</returns>
    internal static string ScheduleWording(SessionTaskModel task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return SessionTaskModel.DesktopOnlySentence + " " + task.NextRunText + " " + task.LastRunText + " " + task.MissedRunsText;
    }

    private async Task<CoworkWebCall<T>> SendAsync<T>(HttpMethod method, string path, object? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (content is not null)
        {
            request.Content = JsonContent.Create(content, options: CoworkProjectJsonContext.Default.Options);
        }

        try
        {
            using var response = await _http.Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode == 401)
            {
                return new CoworkWebCall<T>(false, CoworkDegradedSentences.TokenExpired, default);
            }

            if ((int)response.StatusCode == 404)
            {
                return new CoworkWebCall<T>(false, CoworkDegradedSentences.ApiDisabled, default);
            }

            if ((int)response.StatusCode == 429)
            {
                return new CoworkWebCall<T>(false, CoworkDegradedSentences.RateLimited, default);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new CoworkWebCall<T>(false, "The request failed.", default);
            }

            if (typeof(T) == typeof(bool))
            {
                return new CoworkWebCall<T>(true, null, (T)(object)true);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(CoworkProjectJsonContext.Default.Options, cancellationToken).ConfigureAwait(false);
            return new CoworkWebCall<T>(true, null, value);
        }
        catch (HttpRequestException)
        {
            return new CoworkWebCall<T>(false, CoworkDegradedSentences.Offline, default);
        }
    }
}
