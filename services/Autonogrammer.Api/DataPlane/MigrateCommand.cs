using System.Globalization;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres;
using Autonogrammer.Sessions.Postgres.Migrations;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// The <c>migrate</c> verb of the API entry point: <c>dotnet Autonogrammer.Api.dll migrate</c> applies every pending migration step to the compiled head and
/// <c>migrate --down &lt;version&gt;</c> reverts the steps above a version. It runs under the migrator role (<c>Sessions:Postgres:MigratorConnectionString</c>) and
/// nothing else: it builds its own minimal configuration (appsettings, environment, command line) and registers no Kestrel, hosting, authentication or
/// harness option. The exit code is 0 when the schema was migrated or already current, 1 when the run failed and 2 for a usage error. Output names keys and
/// exception types only; no connection string reaches it.
/// </summary>
internal static class MigrateCommand
{
    /// <summary>Gets the verb.</summary>
    public const string Verb = "migrate";

    /// <summary>Gets the exit code of a run that applied the steps or found the schema current.</summary>
    public const int Succeeded = 0;

    /// <summary>Gets the exit code of a run that failed.</summary>
    public const int Failed = 1;

    /// <summary>Gets the exit code of a usage error.</summary>
    public const int Usage = 2;

    private const string DownOption = "--down";

    /// <summary>Returns whether <paramref name="args"/> start with the migrate verb.</summary>
    /// <param name="args">The command-line arguments of the process.</param>
    /// <returns><see langword="true"/> when the first argument is <c>migrate</c>.</returns>
    public static bool IsMigrate(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Length > 0 && string.Equals(args[0], Verb, StringComparison.Ordinal);
    }

    /// <summary>Runs the verb against the configuration the process starts with.</summary>
    /// <param name="args">The command-line arguments, the first of which is <c>migrate</c>.</param>
    /// <param name="output">Receives the progress lines.</param>
    /// <param name="error">Receives the failure lines.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var downTo, out var overrides))
        {
            await error.WriteLineAsync("Usage: migrate [--down <version>] [--Key=Value ...]").ConfigureAwait(false);
            return Usage;
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine([.. overrides])
            .Build();
        return await RunAsync(configuration, downTo, output, error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs the verb against an explicit configuration.</summary>
    /// <param name="configuration">The configuration: <c>Sessions:Postgres:MigratorConnectionString</c> and the <c>DataPlane:Postgres</c> section.</param>
    /// <param name="downTo">The version to keep when reverting, or <see langword="null"/> to migrate up to the head.</param>
    /// <param name="output">Receives the progress lines.</param>
    /// <param name="error">Receives the failure lines.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(IConfiguration configuration, int? downTo, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var connectionString = configuration[Autonogrammer.Sessions.Postgres.ServiceCollectionExtensions.MigratorConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await error.WriteLineAsync($"{Autonogrammer.Sessions.Postgres.ServiceCollectionExtensions.MigratorConnectionStringKey} is required for the migrate verb.").ConfigureAwait(false);
            return Failed;
        }

        var resilienceOptions = configuration.GetSection(PostgresResilienceOptions.SectionName).Get<PostgresResilienceOptions>() ?? new PostgresResilienceOptions();
        try
        {
            var dataSource = new PostgresSessionConnection(connectionString).CreateDataSource(PostgresRole.Migrator, resilienceOptions);
            await using (dataSource.ConfigureAwait(false))
            {
                var runner = new MigrationRunner(dataSource, resilience: PostgresResilience.Create(resilienceOptions));
                if (downTo is { } version)
                {
                    var reverted = await runner.DownAsync(version, cancellationToken).ConfigureAwait(false);
                    await output.WriteLineAsync($"Reverted {reverted.Count.ToString(CultureInfo.InvariantCulture)} migration(s); the schema is at version {(await runner.GetCurrentVersionAsync(cancellationToken).ConfigureAwait(false)).ToString(CultureInfo.InvariantCulture)}.").ConfigureAwait(false);
                    return Succeeded;
                }

                var applied = await runner.MigrateAsync(cancellationToken).ConfigureAwait(false);
                await output.WriteLineAsync($"Applied {applied.Count.ToString(CultureInfo.InvariantCulture)} migration(s); the schema is at head version {runner.Head.ToString(CultureInfo.InvariantCulture)}.").ConfigureAwait(false);
                return Succeeded;
            }
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("The migrate verb was cancelled.").ConfigureAwait(false);
            return Failed;
        }
#pragma warning disable CA1031 // The verb ends with one exit code; the message names the exception type, because a driver message can quote the connection.
        catch (Exception ex)
        {
            var detail = ex is MigrationChecksumMismatchException or MigrationPreconditionException or DataPlaneTimeoutException
                ? ex.Message
                : "see the exception type";
            await error.WriteLineAsync($"The migrate verb failed ({ex.GetType().Name}): {detail}").ConfigureAwait(false);
            return Failed;
        }
#pragma warning restore CA1031
    }

    private static bool TryParse(string[] args, out int? downTo, out List<string> overrides)
    {
        downTo = null;
        overrides = [];
        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, DownOption, StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length
                    || !int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    return false;
                }

                downTo = version;
                index++;
            }
            else if (argument.StartsWith("--", StringComparison.Ordinal) && argument.Contains('=', StringComparison.Ordinal))
            {
                overrides.Add(argument);
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}
