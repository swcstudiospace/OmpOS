using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Services;
using Autonogrammer.Coordination;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Autonogrammer.Sandbox;
using Autonogrammer.Sessions.Postgres;
using Npgsql;
using StackExchange.Redis;

namespace Autonogrammer.Api.Operations;

/// <summary>
/// The <c>erase-account</c> verb of the API entry point: <c>dotnet Autonogrammer.Api.dll erase-account &lt;account-id&gt;</c> erases everything one account owns
/// through <see cref="PostgresAccountErasure"/> (runtime role), then the account's tenant keys in Dragonfly when <c>Coordination:Store</c> is Dragonfly, then its workspace folder under <c>Api:Workspace:Root</c>,
/// then the account's workspace folder on every worker of <c>Cloud:Environment:StaticPool:Workers</c> and, when <c>Cloud:Environment:Provider</c> is Railway, on every worker service Railway lists, each purged with a signed purge token.
/// It is operator-only: it runs before the web host exists and no HTTP route reaches it. The operator token is read from the environment variable
/// <c>AUTONOGRAMMER_OPERATOR_TOKEN</c>; its SHA-256 must equal the digest configured at <c>Api:Operator:TokenSha256</c> (environment form
/// <c>AUTONOGRAMMER_OPERATOR_TOKEN_SHA256</c>). The check runs before any connection is opened.
/// The exit code is 0 when the account was erased, 1 when the erasure failed or a required setting is absent, 2 for a usage error and 3 when the operator token
/// is absent or does not match. Output names keys and exception types only; neither the token, the digest nor a connection string reaches it.
/// </summary>
internal static class EraseAccountCommand
{
    /// <summary>Gets the verb.</summary>
    public const string Verb = "erase-account";

    /// <summary>Gets the environment variable that carries the operator token.</summary>
    public const string TokenEnvironmentVariable = "AUTONOGRAMMER_OPERATOR_TOKEN";

    /// <summary>Gets the environment variable that carries the SHA-256 digest of the operator token. It is bound to <see cref="TokenSha256Key"/>.</summary>
    public const string TokenSha256EnvironmentVariable = "AUTONOGRAMMER_OPERATOR_TOKEN_SHA256";

    /// <summary>Gets the configuration key of the SHA-256 digest of the operator token.</summary>
    public const string TokenSha256Key = "Api:Operator:TokenSha256";

    /// <summary>Gets the exit code of a run that erased the account.</summary>
    public const int Succeeded = 0;

    /// <summary>Gets the exit code of a run that failed.</summary>
    public const int Failed = 1;

    /// <summary>Gets the exit code of a usage error.</summary>
    public const int Usage = 2;

    /// <summary>Gets the exit code of a run whose operator token is absent or does not match the configured digest.</summary>
    public const int Refused = 3;

    /// <summary>Gets how long one purge request to a worker may take.</summary>
    private static readonly TimeSpan PurgeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Returns whether <paramref name="args"/> start with the erase-account verb.</summary>
    /// <param name="args">The command-line arguments of the process.</param>
    /// <returns><see langword="true"/> when the first argument is <c>erase-account</c>.</returns>
    public static bool IsEraseAccount(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Length > 0 && string.Equals(args[0], Verb, StringComparison.Ordinal);
    }

    /// <summary>Runs the verb: checks the operator token, then erases the account.</summary>
    /// <param name="args">The command-line arguments: the verb and the account identifier.</param>
    /// <param name="output">Receives the progress lines.</param>
    /// <param name="error">Receives the failure lines.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            await error.WriteLineAsync($"Usage: {Verb} <account-id>").ConfigureAwait(false);
            return Usage;
        }

        var configuration = BuildConfiguration();
        var refusal = RefusalFor(Environment.GetEnvironmentVariable(TokenEnvironmentVariable), configuration[TokenSha256Key]);
        if (refusal is not null)
        {
            await error.WriteLineAsync(refusal).ConfigureAwait(false);
            return Refused;
        }

        return await EraseAsync(configuration, args[1], output, error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns why the operator token is refused, or <see langword="null"/> when its SHA-256 equals the configured digest.</summary>
    /// <param name="token">The operator token from the environment, or null when it is absent.</param>
    /// <param name="digestText">The configured digest as 64 hexadecimal characters, or null when it is absent.</param>
    /// <returns>The refusal message, or <see langword="null"/> when the token is accepted.</returns>
    internal static string? RefusalFor(string? token, string? digestText)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return $"Refused: {TokenEnvironmentVariable} is not set.";
        }

        if (string.IsNullOrWhiteSpace(digestText))
        {
            return $"Refused: {TokenSha256Key} is not configured (environment {TokenSha256EnvironmentVariable}).";
        }

        if (!TryDecodeDigest(digestText.Trim(), out var expected))
        {
            return $"Refused: {TokenSha256Key} is not a SHA-256 digest (64 hexadecimal characters).";
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(actual, expected)
            ? null
            : "Refused: the operator token does not match the configured digest.";
    }

    private static bool TryDecodeDigest(string text, out byte[] digest)
    {
        digest = [];
        if (text.Length != SHA256.HashSizeInBytes * 2)
        {
            return false;
        }

        try
        {
            digest = Convert.FromHexString(text);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var overrides = new List<KeyValuePair<string, string?>>();
        var digest = Environment.GetEnvironmentVariable(TokenSha256EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(digest))
        {
            overrides.Add(new KeyValuePair<string, string?>(TokenSha256Key, digest));
        }

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(overrides)
            .Build();
    }

    private static async Task<int> EraseAsync(IConfiguration configuration, string accountId, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var connectionString = configuration[Autonogrammer.Sessions.Postgres.ServiceCollectionExtensions.ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await error.WriteLineAsync($"{Autonogrammer.Sessions.Postgres.ServiceCollectionExtensions.ConnectionStringKey} is required for the {Verb} verb.").ConfigureAwait(false);
            return Failed;
        }

        var dragonfly = CoordinationOptions.IsDragonflyStore(configuration[$"{CoordinationOptions.SectionName}:Store"]);
        var dragonflyConnectionString = configuration[CoordinationOptions.DragonflyConnectionStringKey];
        if (dragonfly && string.IsNullOrWhiteSpace(dragonflyConnectionString))
        {
            await error.WriteLineAsync($"{CoordinationOptions.DragonflyConnectionStringKey} is required for the {Verb} verb when Coordination:Store is Dragonfly.").ConfigureAwait(false);
            return Failed;
        }

        var pepper = configuration[DataPlaneOptions.TenantHashPepperKey];
        if (string.IsNullOrWhiteSpace(pepper))
        {
            if (dragonfly)
            {
                await error.WriteLineAsync($"{DataPlaneOptions.TenantHashPepperKey} is required for the {Verb} verb when Coordination:Store is Dragonfly.").ConfigureAwait(false);
                return Failed;
            }

            // The in-memory store holds no tenant-keyed state across processes, so any pepper yields a well-formed segment.
            pepper = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        }

        var tenantHash = new TenantHasher(new TenantHasherOptions { Pepper = pepper }).HashTenant(accountId);
        var resilienceOptions = configuration.GetSection(PostgresResilienceOptions.SectionName).Get<PostgresResilienceOptions>() ?? new PostgresResilienceOptions();
        var dragonflyResilience = DragonflyResilience.Create(configuration.GetSection(DragonflyResilienceOptions.SectionName).Get<DragonflyResilienceOptions>());
        var workspaceRoot = configuration[$"{Autonogrammer.Api.Services.SessionWorkspaceOptions.SectionName}:Root"];
        var cloud = configuration.GetSection(CloudOptions.SectionName).Get<CloudOptions>() ?? new CloudOptions();
        var request = new ErasureRequest(connectionString, accountId, tenantHash, dragonfly, dragonflyConnectionString, resilienceOptions, dragonflyResilience, workspaceRoot, cloud);
        return await RunErasureAsync(request, output, error, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> RunErasureAsync(ErasureRequest request, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            await EraseDataAsync(request, cancellationToken).ConfigureAwait(false);
            var purged = await PurgeWorkersAsync(request, error, cancellationToken).ConfigureAwait(false);
            if (purged is null)
            {
                return Failed;
            }

            await output.WriteLineAsync("Erased the account and its tenant keys.").ConfigureAwait(false);
            if (purged.Value > 0)
            {
                await output.WriteLineAsync($"Purged the account's workspace from {purged.Value} worker(s).").ConfigureAwait(false);
            }

            return Succeeded;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync($"The {Verb} verb was cancelled.").ConfigureAwait(false);
            return Failed;
        }
        catch (PostgresException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (NpgsqlException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (AccountErasureLockTimeoutException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (DataPlaneTimeoutException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (WorkerPurgeException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return await ReportFailureAsync(error, ex).ConfigureAwait(false);
        }
    }

    private static async Task EraseDataAsync(ErasureRequest request, CancellationToken cancellationToken)
    {
        var dataSource = new PostgresSessionConnection(request.ConnectionString).CreateDataSource(PostgresRole.Runtime, request.ResilienceOptions);
        await using (dataSource.ConfigureAwait(false))
        {
            var erasure = new PostgresAccountErasure(dataSource, PostgresResilience.Create(request.ResilienceOptions));
            if (request.Dragonfly)
            {
                using var multiplexer = ConnectDragonfly(request.DragonflyConnectionString!, request.DragonflyResilience);
                var keys = new DragonflyTenantErasure(multiplexer, request.DragonflyResilience);
                await EraseAccountAsync(erasure, request, keys.DeleteTenantKeysAsync, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var keys = new NoOpTenantKeyEraser();
                await EraseAccountAsync(erasure, request, keys.DeleteTenantKeysAsync, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<int?> PurgeWorkersAsync(ErasureRequest request, TextWriter error, CancellationToken cancellationToken)
    {
        var workers = request.Cloud.Environment.StaticPool.Workers;
        var railway = IsRailway(request.Cloud);
        if (workers.Count == 0 && !railway)
        {
            return 0;
        }

        var ownerDir = AccountWorkspace.DirectoryName(request.AccountId);
        using var issuer = new WorkerTokenIssuer(Microsoft.Extensions.Options.Options.Create(request.Cloud), TimeProvider.System);
        using var http = new HttpClient { Timeout = PurgeTimeout };
        var client = new WorkerPurgeClient(http, issuer);
        foreach (var worker in workers)
        {
            _ = await client.PurgeAsync(WorkerBaseUri(worker.BaseUri), ownerDir, cancellationToken).ConfigureAwait(false);
        }

        if (!railway)
        {
            return workers.Count;
        }

        var discovered = await PurgeRailwayWorkersAsync(token => DiscoverRailwayWorkersAsync(request.Cloud, token), client, ownerDir, error, cancellationToken).ConfigureAwait(false);
        return discovered is null ? (int?)null : workers.Count + discovered.Value;
    }

    private static bool IsRailway(CloudOptions cloud) =>
        string.Equals(cloud.Environment.Provider, CloudOptions.EnvironmentSettings.RailwayProvider, StringComparison.OrdinalIgnoreCase);

    private static async Task<IReadOnlyList<Uri>> DiscoverRailwayWorkersAsync(CloudOptions cloud, CancellationToken cancellationToken)
    {
        using var graphQl = new HttpClient { Timeout = PurgeTimeout };
        using var probe = new HttpClient { Timeout = PurgeTimeout };
        var client = new RailwayCloudEnvironmentClient(graphQl, probe, RailwayAccess(cloud.Environment.Railway), TimeProvider.System);
        return await client.ListWorkerBaseUrisAsync(cancellationToken).ConfigureAwait(false);
    }

    private static RailwayCloudEnvironmentOptions RailwayAccess(CloudOptions.RailwaySettings railway)
    {
        var options = new RailwayCloudEnvironmentOptions
        {
            ApiToken = railway.ApiToken,
            ProjectId = railway.ProjectId,
            EnvironmentId = railway.EnvironmentId,
            Image = railway.Image,
            ServiceNamePrefix = railway.WorkerNamePrefix,
        };
        if (!string.IsNullOrWhiteSpace(railway.Endpoint))
        {
            options.Endpoint = new Uri(railway.Endpoint);
        }

        return options;
    }

    /// <summary>
    /// Purges the account's folder on every Railway worker that <paramref name="discover"/> returns. Every worker is tried; a worker that fails is named by its
    /// authority and counted, so erasure fails when discovery or any purge failed and a re-run retries it.
    /// </summary>
    /// <param name="discover">Lists the worker base addresses.</param>
    /// <param name="client">Sends each purge.</param>
    /// <param name="ownerDir">The account's workspace directory name.</param>
    /// <param name="error">Receives one line per failure; it names the host and exception type only.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The number of workers purged, or null when discovery or any purge failed.</returns>
    internal static async Task<int?> PurgeRailwayWorkersAsync(
        Func<CancellationToken, Task<IReadOnlyList<Uri>>> discover,
        WorkerPurgeClient client,
        string ownerDir,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Uri> workers;
        try
        {
            workers = await discover(cancellationToken).ConfigureAwait(false);
        }
        catch (CloudEnvironmentException ex)
        {
            await error.WriteLineAsync($"The {Verb} verb could not list the Railway workers: {ex.Failure}.").ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            await error.WriteLineAsync($"The {Verb} verb could not build the Railway client from Cloud:Environment:Railway: {ex.GetType().Name}.").ConfigureAwait(false);
            return null;
        }

        var failed = 0;
        foreach (var worker in workers)
        {
            try
            {
                _ = await client.PurgeAsync(worker, ownerDir, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WorkerPurgeException or HttpRequestException)
            {
                failed++;
                await error.WriteLineAsync($"The {Verb} verb could not purge the account's workspace on worker {worker.Authority}: {ex.GetType().Name}.").ConfigureAwait(false);
            }
        }

        return failed == 0 ? workers.Count : (int?)null;
    }

    private static Uri WorkerBaseUri(string baseUri) =>
        Uri.TryCreate(baseUri, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https")
            ? uri
            : throw new InvalidOperationException("Cloud:Environment:StaticPool:Workers BaseUri must be an absolute http or https URI.");

    private static Task EraseAccountAsync(PostgresAccountErasure erasure, ErasureRequest request, Func<string, CancellationToken, Task> eraseTenantKeys, CancellationToken cancellationToken) =>
        erasure.EraseAsync(request.AccountId, request.TenantHash, async (tenantHash, token) =>
        {
            await eraseTenantKeys(tenantHash, token).ConfigureAwait(false);
            await AccountWorkspaceEraser.DeleteAsync(request.WorkspaceRoot, request.AccountId, token).ConfigureAwait(false);
        }, cancellationToken);

    private static async Task<int> ReportFailureAsync(TextWriter error, Exception exception)
    {
        await error.WriteLineAsync($"The {Verb} verb failed: {exception.GetType().Name}.").ConfigureAwait(false);
        return Failed;
    }


    private static ConnectionMultiplexer ConnectDragonfly(string connectionString, DragonflyResilience resilience)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AsyncTimeout = resilience.Options.OpMs;
        options.ConnectTimeout = resilience.Options.ConnectSeconds * 1000;
        return ConnectionMultiplexer.Connect(options);
    }

    /// <summary>The inputs of one erasure run.</summary>
    private sealed record ErasureRequest(string ConnectionString, string AccountId, string TenantHash, bool Dragonfly, string? DragonflyConnectionString, PostgresResilienceOptions ResilienceOptions, DragonflyResilience DragonflyResilience, string? WorkspaceRoot, CloudOptions Cloud);
}
