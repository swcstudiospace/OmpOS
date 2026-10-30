using System.Runtime.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.AgentWorker.Tests;

/// <summary>
/// A real worker on a loopback Kestrel port, configured with a fake omp script, a fresh workspace root, the public key of a
/// <see cref="WorkerTokenFactory"/> and a <see cref="FixedTimeProvider"/>. Nothing leaves the machine.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class WorkerHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private WorkerHost(WebApplication app, string directory, string ompPath, Uri address, IReadOnlyList<string> registeredServiceTypes)
    {
        _app = app;
        RegisteredServiceTypes = registeredServiceTypes;
        Root = directory;
        OmpPath = ompPath;
        Address = address;
    }

    /// <summary>Gets the temporary directory holding the workspace root, the fake omp and the files the fake writes.</summary>
    public string Root { get; }

    /// <summary>Gets the absolute path of the fake omp script.</summary>
    public string OmpPath { get; }

    /// <summary>Gets the loopback base address, for example <c>http://127.0.0.1:41000</c>.</summary>
    public Uri Address { get; }

    /// <summary>Gets the workspace root the worker was configured with.</summary>
    public string WorkspaceRoot => Path.Combine(Root, "work");

    /// <summary>Gets the WebSocket address of the bridge.</summary>
    public Uri AcpUri => new UriBuilder(Address) { Scheme = "ws", Path = "/acp" }.Uri;

    /// <summary>Gets the full names of every service type registered when the worker was built.</summary>
    public IReadOnlyList<string> RegisteredServiceTypes { get; }

    /// <summary>Gets the services of the running worker.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>Starts a worker.</summary>
    /// <param name="tokens">The control plane whose public key the worker trusts.</param>
    /// <param name="clock">The clock the worker validates token lifetimes with.</param>
    /// <param name="ompArguments">The arguments the worker appends after <c>acp</c>.</param>
    /// <param name="logs">Optional sink for every log line the worker writes.</param>
    /// <param name="settings">Optional configuration overrides, keyed like environment variables with <c>:</c>.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running host. The caller disposes it.</returns>
    public static async Task<WorkerHost> StartAsync(
        WorkerTokenFactory tokens,
        FixedTimeProvider clock,
        IReadOnlyList<string>? ompArguments = null,
        CapturingLoggerProvider? logs = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        CancellationToken cancellationToken = default)
    {
        LinuxOnly.Require();
        var directory = System.IO.Directory.CreateTempSubdirectory("autonogrammer-worker-").FullName;
        var ompPath = FakeOmpScript.Write(directory);
        var configuration = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Worker:WorkspaceRoot"] = Path.Combine(directory, "work"),
            ["Worker:Omp:Path"] = ompPath,
            ["Worker:Auth:PublicKeysPem:0"] = tokens.PublicKeyPem,
            ["Worker:CloseTimeout"] = "00:00:05",
        };
        var arguments = ompArguments ?? [];
        for (var index = 0; index < arguments.Count; index++)
        {
            configuration["Worker:Omp:ExtraArguments:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)] = arguments[index];
        }

        if (settings is not null)
        {
            foreach (var (key, value) in settings)
            {
                configuration[key] = value;
            }
        }

        IReadOnlyList<string> registeredServiceTypes = [];
        var app = WorkerApp.Build(
            [],
            builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Configuration.AddInMemoryCollection(configuration);
                builder.Services.AddSingleton<TimeProvider>(clock);
                registeredServiceTypes = [.. builder.Services.Select(static descriptor => descriptor.ServiceType.FullName ?? descriptor.ServiceType.Name)];
                if (logs is not null)
                {
                    builder.Logging.AddProvider(logs);
                }
            });
        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await app.DisposeAsync().ConfigureAwait(false);
            System.IO.Directory.Delete(directory, recursive: true);
            throw;
        }

        return new WorkerHost(app, directory, ompPath, new Uri(app.Urls.First(), UriKind.Absolute), registeredServiceTypes);
    }

    /// <summary>Stops the host, as a platform shutdown would.</summary>
    /// <param name="cancellationToken">Cancels the wait for the shutdown.</param>
    /// <returns>A task that completes when the host has stopped.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => _app.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
        System.IO.Directory.Delete(Root, recursive: true);
    }
}
