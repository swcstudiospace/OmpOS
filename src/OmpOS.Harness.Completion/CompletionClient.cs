using System.Net.Http.Json;

namespace OmpOS.Harness.Completion;

/// <summary>
/// Requests inline completions from a configured model provider.
/// </summary>
/// <remarks>
/// <para>
/// With no provider configured, <see cref="SuggestAsync"/> returns <see cref="CompletionResult.Disabled"/> and does not
/// touch the network. With a provider configured, each call posts exactly one JSON request to the endpoint:
/// <c>{"prefix":"…","suffix":"…"}</c>, the text before and after the caret. The provider answers with
/// <c>{"suggestion":"…"}</c>; a missing or empty suggestion yields <see cref="CompletionStatus.NoSuggestion"/>.
/// </para>
/// <para>
/// The client does not retry. A non-success status raises <see cref="HttpRequestException"/>, which the caller shows.
/// </para>
/// </remarks>
public sealed class CompletionClient
{
    private readonly HttpClient _http;

    /// <summary>Creates a client.</summary>
    /// <param name="settings">The completion settings. Completion is off unless they configure an endpoint.</param>
    /// <param name="http">The HTTP client that posts requests. Its lifetime belongs to the caller.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> or <paramref name="http"/> is null.</exception>
    public CompletionClient(CompletionSettings settings, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(http);

        Settings = settings;
        _http = http;
    }

    /// <summary>Gets the settings this client was created with.</summary>
    public CompletionSettings Settings { get; }

    /// <summary>Asks the provider for a suggestion at the caret.</summary>
    /// <param name="context">The text and caret to complete at.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// <see cref="CompletionResult.Disabled"/> when no provider is configured, with no request sent; otherwise the
    /// provider's suggestion, or <see cref="CompletionResult.NoSuggestion"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="HttpRequestException">The provider answered with a non-success status or could not be reached.</exception>
    public async Task<CompletionResult> SuggestAsync(CaretBuffer context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Settings.Endpoint is not { } endpoint)
        {
            return CompletionResult.Disabled;
        }

        var body = new CompletionRequestBody(
            Prefix: context.Text[..context.Caret],
            Suffix: context.Text[context.Caret..]);

        using var response = await _http
            .PostAsJsonAsync(endpoint, body, CompletionJsonContext.Default.CompletionRequestBody, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var answer = await response.Content
            .ReadFromJsonAsync(CompletionJsonContext.Default.CompletionResponseBody, cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrEmpty(answer?.Suggestion)
            ? CompletionResult.NoSuggestion
            : CompletionResult.Suggested(answer.Suggestion);
    }
}
