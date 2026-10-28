using System.Globalization;
using Autonogrammer.Harness.Completion;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The Tab page's status from <see cref="CompletionSettingsLoader"/>: whether inline completion is on, the provider's host and port
/// (never the full address, which may carry a credential) and which variable configures it. The page is read-only.
/// </summary>
internal sealed class TabSettingsSource : ITabSettingsSource
{
    private readonly Func<string, string?> _getVariable;

    /// <summary>Initializes a new instance of the <see cref="TabSettingsSource"/> class over the process environment.</summary>
    public TabSettingsSource()
        : this(static name => Environment.GetEnvironmentVariable(name))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TabSettingsSource"/> class over an injected environment.</summary>
    /// <param name="getVariable">Reads an environment variable, returning null when it is unset.</param>
    /// <exception cref="ArgumentNullException"><paramref name="getVariable"/> is null.</exception>
    public TabSettingsSource(Func<string, string?> getVariable)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        _getVariable = getVariable;
    }

    /// <inheritdoc/>
    public Task<TabSettingsValues> LoadAsync(CancellationToken cancellationToken) => SettingsTask.ReadAsync(Read);

    private TabSettingsValues Read()
    {
        CompletionSettings settings;
        try
        {
            settings = new CompletionSettingsLoader(_getVariable).Load();
        }
        catch (InvalidDataException exception)
        {
            // The loader's message names the variable or the file and never repeats the configured value.
            throw new SettingsSourceException(string.Create(CultureInfo.InvariantCulture, $"{HeadSettingsCopy.CompletionUnreadable} {exception.Message}"), exception);
        }

        var source = IsSet(CompletionSettingsLoader.EndpointVariableName) || !IsSet(CompletionSettingsLoader.FileVariableName)
            ? HeadSettingsCopy.CompletionEndpointSource
            : HeadSettingsCopy.CompletionFileSource;
        return settings.IsConfigured
            ? new TabSettingsValues(Enabled: true, HostAndPort(settings.Endpoint), source)
            : new TabSettingsValues(Enabled: false, EndpointHost: null, source);
    }

    private bool IsSet(string name) => !string.IsNullOrWhiteSpace(_getVariable(name));

    private static string HostAndPort(Uri endpoint) => endpoint.IsDefaultPort
        ? endpoint.Host
        : string.Create(CultureInfo.InvariantCulture, $"{endpoint.Host}:{endpoint.Port}");
}
