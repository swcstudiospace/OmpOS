using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Harness.Completion;

/// <summary>
/// Builds <see cref="CompletionSettings"/> from the user's configuration: environment variables or a JSON file. With nothing
/// configured the result is <see cref="CompletionSettings.None"/>; a default endpoint is never filled in.
/// </summary>
/// <remarks>
/// <para>
/// Sources, in order: the <see cref="EndpointVariableName"/> environment variable, then the JSON file named by the
/// <see cref="FileVariableName"/> environment variable. The first source that sets an endpoint wins. An unset or white space
/// variable counts as not set. The file is <c>{"endpoint":"https://provider.example/complete"}</c>; other fields are ignored,
/// and a file without an <c>endpoint</c> field configures nothing.
/// </para>
/// <para>
/// A configured source that cannot be used fails loudly with <see cref="InvalidDataException"/>: a named file that is missing
/// or unreadable, a file that is not JSON of the expected shape, and an endpoint that is not an absolute <c>http</c> or
/// <c>https</c> URI. The message names the file or variable and the field, and never repeats the configured value, which may
/// carry credentials.
/// </para>
/// </remarks>
public sealed class CompletionSettingsLoader
{
    /// <summary>The environment variable that holds the completion provider endpoint.</summary>
    public const string EndpointVariableName = "AUTONOGRAMMER_COMPLETION_ENDPOINT";

    /// <summary>The environment variable that names the JSON configuration file.</summary>
    public const string FileVariableName = "AUTONOGRAMMER_COMPLETION_CONFIG";

    /// <summary>The field of the configuration file that holds the endpoint.</summary>
    public const string EndpointFieldName = "endpoint";

    private readonly Func<string, string?> _getEnvironmentVariable;

    /// <summary>Initializes a new instance of the <see cref="CompletionSettingsLoader"/> class that reads the real environment.</summary>
    public CompletionSettingsLoader()
        : this(static name => Environment.GetEnvironmentVariable(name))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CompletionSettingsLoader"/> class with an injected environment.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable, returning null when it is unset.</param>
    /// <exception cref="ArgumentNullException"><paramref name="getEnvironmentVariable"/> is null.</exception>
    public CompletionSettingsLoader(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    /// <summary>Loads the settings from the environment and the file it names.</summary>
    /// <returns>The configured settings, or <see cref="CompletionSettings.None"/> when nothing is configured.</returns>
    /// <exception cref="InvalidDataException">A configured source is missing, unreadable or invalid.</exception>
    public CompletionSettings Load()
    {
        if (ReadVariable(EndpointVariableName) is { } endpoint)
        {
            return Parse(endpoint, $"environment variable {EndpointVariableName}");
        }

        return ReadVariable(FileVariableName) is { } path
            ? LoadFile(path)
            : CompletionSettings.None;
    }

    /// <summary>Loads the settings from a JSON file.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The configured settings, or <see cref="CompletionSettings.None"/> when the file sets no endpoint.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    /// <exception cref="InvalidDataException">The file is missing, unreadable, not valid JSON of the expected shape, or holds an invalid endpoint.</exception>
    public static CompletionSettings LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The completion configuration file '{path}' cannot be read: {exception.Message}"),
                exception);
        }

        CompletionFileBody? body;
        try
        {
            body = JsonSerializer.Deserialize(text, CompletionJsonContext.Default.CompletionFileBody);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The completion configuration file '{path}' is not valid JSON of the form {{\"{EndpointFieldName}\":\"<url>\"}}."),
                exception);
        }

        return body?.Endpoint is { } endpoint
            ? Parse(endpoint, $"file '{path}' field '{EndpointFieldName}'")
            : CompletionSettings.None;
    }

    private static CompletionSettings Parse(string value, string where)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            && (string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                || string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return CompletionSettings.ForEndpoint(endpoint);
        }

        throw new InvalidDataException(
            string.Create(CultureInfo.InvariantCulture, $"The completion endpoint in {where} is not an absolute http or https URI."));
    }

    private string? ReadVariable(string name)
    {
        var value = _getEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
