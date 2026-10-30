using Autonogrammer.Harness.OmpConfig;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Providers;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Presentation.Settings.Models;

namespace Autonogrammer.Native.Settings.Models;

/// <summary>
/// The Models page's source and the provider keys of the processes the head starts, built together over one key store, so a key saved
/// on the page is the key the next omp receives. The composition owns the HTTP client of the connection test; dispose it with the head.
/// </summary>
internal sealed class ModelsSettingsComposition : IDisposable
{
    /// <summary>The name of the key store file below the secrets directory of the state root.</summary>
    public const string SecretsFileName = "secrets.json";

    /// <summary>The name of the directory below the state root that holds the key store.</summary>
    public const string SecretsDirectoryName = "secrets";

    /// <summary>The name of the file below the state root that holds the display name and routes of each custom provider.</summary>
    public const string DetailsFileName = "custom-providers.json";

    private readonly HttpClient _client;
    private readonly ModelsSettingsSource _source;

    private ModelsSettingsComposition(ModelsSettingsCompositionOptions options)
    {
        var clock = options.TimeProvider ?? TimeProvider.System;
        var omp = new OmpModelConfiguration(new OmpConfigOptions
        {
            Locator = options.OmpLocator ?? OmpLocatorOptions.FromEnvironment(options.OmpPath),
            AgentDirectory = options.AgentDirectory,
            Home = options.Home,
            TimeProvider = options.TimeProvider,
            CommandTimeout = options.CommandTimeout ?? OmpConfigOptions.DefaultCommandTimeout,
        });
        Omp = omp;
        var secrets = options.SecretStore
            ?? new OwnerOnlyFileSecretStore(Path.Combine(options.StateRoot, SecretsDirectoryName, SecretsFileName));
        Keys = new ProviderKeyEnvironment(secrets);
        _client = CreateClient(options.ProbeHandler);
        _source = new ModelsSettingsSource(
            omp,
            secrets,
            Keys,
            new ProviderRegistryStore(Path.Combine(options.StateRoot, DetailsFileName)),
            new LocalProviderProbe(_client, clock),
            new ProviderErrorMapper(clock));
    }

    /// <summary>Gets the source the Models page reads and writes through.</summary>
    public IModelsSettingsSource Source => _source;

    /// <summary>Gets the model configuration of omp the source reads, so the Network page's diagnostics list the endpoints of the managed providers from the same <c>models.yml</c>.</summary>
    public OmpModelConfiguration Omp { get; }

    /// <summary>Gets the provider keys the head merges into the environment of every omp it starts.</summary>
    public ProviderKeyEnvironment Keys { get; }

    /// <summary>Builds the source and the provider keys. Nothing is read or started until the source or <see cref="ProviderKeyEnvironment.RefreshAsync"/> is used.</summary>
    /// <param name="options">Where the state is kept, how omp is found, and the test seams.</param>
    /// <returns>The composition. The caller disposes it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The state root is blank.</exception>
    public static ModelsSettingsComposition Create(ModelsSettingsCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.StateRoot))
        {
            throw new ArgumentException("The state root must not be blank.", nameof(options));
        }

        return new ModelsSettingsComposition(options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _source.Dispose();
        _client.Dispose();
    }

    private static HttpClient CreateClient(HttpMessageHandler? handler)
    {
        if (handler is not null)
        {
            return new HttpClient(handler, disposeHandler: false);
        }

        var own = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
        try
        {
            var client = new HttpClient(own, disposeHandler: true);
            own = null;
            return client;
        }
        finally
        {
            own?.Dispose();
        }
    }
}
