using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmpOS.Api.Services.Providers;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>Builds hosts whose provider services run over fakes: no omp, no environment variable of the test machine and no network.</summary>
internal static class ProvidersTestHost
{
    /// <summary>Creates a Development host (development principal on).</summary>
    /// <param name="backend">The scripted omp.</param>
    /// <param name="settings">The <c>Providers:*</c> overrides, for example <c>AllowCredentialWrite</c>.</param>
    /// <param name="probe">The scripted probe, or null for one that reaches.</param>
    /// <param name="environment">The environment variables that are set, or none.</param>
    /// <param name="logger">An optional capturing log provider.</param>
    /// <returns>The host factory.</returns>
    public static OmpApiFactory Dev(
        FakeProviderBackend backend,
        Dictionary<string, string?>? settings = null,
        FakeProviderProbe? probe = null,
        FakeProviderEnvironment? environment = null,
        ILoggerProvider? logger = null) =>
        WebAppTestHost.Dev(config: Config(settings), more: services => Replace(services, backend, probe, environment), logger: logger);

    /// <summary>Creates a host that validates bearer tokens, so two callers can be told apart.</summary>
    /// <param name="backend">The scripted omp.</param>
    /// <param name="settings">The <c>Providers:*</c> overrides.</param>
    /// <param name="probe">The scripted probe, or null for one that reaches.</param>
    /// <returns>The host factory.</returns>
    public static OmpApiFactory Jwt(FakeProviderBackend backend, Dictionary<string, string?>? settings = null, FakeProviderProbe? probe = null) =>
        WebAppTestHost.Jwt(config: Config(settings), more: services => Replace(services, backend, probe, null));

    /// <summary>Returns the settings that turn every operator switch on.</summary>
    /// <returns>The overrides.</returns>
    public static Dictionary<string, string?> AllOn() => new(StringComparer.Ordinal)
    {
        ["Providers:AllowCredentialWrite"] = "true",
        ["Providers:AllowConfigWrite"] = "true",
        ["Providers:AllowLiveTest"] = "true",
    };

    /// <summary>Sends a JSON request.</summary>
    /// <param name="client">The client.</param>
    /// <param name="method">The method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="json">The body, or null.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? json, CancellationToken ct) =>
        WebAppTestHost.SendAsync(client, method, path, json, ct);

    private static Dictionary<string, string?> Config(Dictionary<string, string?>? settings)
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:StateCacheSeconds"] = "0" };
        foreach (var (key, value) in settings ?? [])
        {
            config[key] = value;
        }

        return config;
    }

    private static void Replace(IServiceCollection services, FakeProviderBackend backend, FakeProviderProbe? probe, FakeProviderEnvironment? environment)
    {
        _ = services.AddSingleton<IProviderBackend>(backend);
        _ = services.AddSingleton<IProviderProbe>(probe ?? new FakeProviderProbe());
        _ = services.AddSingleton<IProviderEnvironment>(environment ?? new FakeProviderEnvironment());
    }
}
