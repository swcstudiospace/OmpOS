using System.Net.Http.Json;

namespace OmpOS.Harness.Completion;

/// <summary>
/// Asks the configured model provider to propose an edit for a selected range of text.
/// </summary>
/// <remarks>
/// <para>
/// With no provider configured, <see cref="ProposeAsync"/> returns <see cref="InlineEditResult.Disabled"/> and does not
/// touch the network. With a provider configured, each call posts exactly one JSON request to the configured endpoint, the
/// same endpoint inline completion uses: <c>{"selection":"…","instruction":"…"}</c>, the text of the selected range and the
/// instruction and no other part of the document. The provider answers with <c>{"replacement":"…"}</c>; a missing
/// replacement yields <see cref="InlineEditOutcome.NoProposal"/>, and an empty one proposes deleting the selection.
/// </para>
/// <para>
/// The client never writes anything. The proposal is a value the caller shows for review. The client does not retry. A
/// non-success status raises <see cref="HttpRequestException"/>, which the caller shows.
/// </para>
/// </remarks>
public sealed class InlineEditClient
{
    private readonly HttpClient _http;

    /// <summary>Creates a client.</summary>
    /// <param name="settings">The completion settings. Inline edit is off unless they configure an endpoint.</param>
    /// <param name="http">The HTTP client that posts requests. Its lifetime belongs to the caller.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> or <paramref name="http"/> is null.</exception>
    public InlineEditClient(CompletionSettings settings, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(http);

        Settings = settings;
        _http = http;
    }

    /// <summary>Gets the settings this client was created with.</summary>
    public CompletionSettings Settings { get; }

    /// <summary>Asks the provider for a replacement of the selected text.</summary>
    /// <param name="selectedText">The text of the selected range. Not empty.</param>
    /// <param name="instruction">What to do to the selection. Not empty or white space.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// <see cref="InlineEditResult.Disabled"/> when no provider is configured, with no request sent; otherwise the
    /// provider's proposal, or <see cref="InlineEditResult.NoProposal"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="selectedText"/> or <paramref name="instruction"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="selectedText"/> is empty, or <paramref name="instruction"/> is empty or white space.</exception>
    /// <exception cref="HttpRequestException">The provider answered with a non-success status or could not be reached.</exception>
    public async Task<InlineEditResult> ProposeAsync(
        string selectedText,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(selectedText);
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);

        if (Settings.Endpoint is not { } endpoint)
        {
            return InlineEditResult.Disabled;
        }

        var body = new InlineEditRequestBody(Selection: selectedText, Instruction: instruction);

        using var response = await _http
            .PostAsJsonAsync(endpoint, body, CompletionJsonContext.Default.InlineEditRequestBody, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var answer = await response.Content
            .ReadFromJsonAsync(CompletionJsonContext.Default.InlineEditResponseBody, cancellationToken)
            .ConfigureAwait(false);

        return answer?.Replacement is { } replacement
            ? InlineEditResult.Proposed(replacement)
            : InlineEditResult.NoProposal;
    }
}
