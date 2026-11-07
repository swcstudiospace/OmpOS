using System.Net.Http.Json;
using Autonogrammer.Contracts.Cowork.Memory;
using Autonogrammer.Presentation.Cowork.Instructions;

namespace Autonogrammer.Web.Services.Cowork;

/// <summary>Memory over <see cref="ApiHttp"/> at <c>/cowork/memory</c>. No browser storage and no desktop cowork assembly reference.</summary>
internal sealed class CoworkMemoryClient
{
    private readonly ApiHttp _http;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The shared API client.</param>
    internal CoworkMemoryClient(ApiHttp http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>Gets <c>/cowork/memory</c>.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The entries.</returns>
    internal async Task<CoworkWebCall<IReadOnlyList<MemoryEntryRecord>>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.Client.GetAsync(new Uri("/cowork/memory", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new CoworkWebCall<IReadOnlyList<MemoryEntryRecord>>(false, Sentence(response.StatusCode), []);
            }

            var value = await response.Content.ReadFromJsonAsync<List<MemoryEntryRecord>>(MemoryJsonContext.Default.Options, cancellationToken).ConfigureAwait(false);
            return new CoworkWebCall<IReadOnlyList<MemoryEntryRecord>>(true, null, value ?? []);
        }
        catch (HttpRequestException)
        {
            return new CoworkWebCall<IReadOnlyList<MemoryEntryRecord>>(false, CoworkDegradedSentences.Offline, []);
        }
    }

    /// <summary>Deletes <c>/cowork/memory/{id}</c>.</summary>
    /// <param name="id">The entry identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The call result.</returns>
    internal async Task<CoworkWebCall<bool>> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.Client.DeleteAsync(new Uri("/cowork/memory/" + Uri.EscapeDataString(id), UriKind.Relative), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? new CoworkWebCall<bool>(true, null, true)
                : new CoworkWebCall<bool>(false, Sentence(response.StatusCode), false);
        }
        catch (HttpRequestException)
        {
            return new CoworkWebCall<bool>(false, CoworkDegradedSentences.Offline, false);
        }
    }

    private static string Sentence(System.Net.HttpStatusCode status) => (int)status switch
    {
        401 => CoworkDegradedSentences.TokenExpired,
        404 => CoworkDegradedSentences.ApiDisabled,
        429 => CoworkDegradedSentences.RateLimited,
        _ => "The request failed.",
    };
}
