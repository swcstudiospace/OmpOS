namespace OmpOS.Workflows.Worker;

/// <summary>
/// Temporal workflow worker host entry point.
/// </summary>
/// <remarks>
/// When <c>Workflows:Enabled=false</c> (default), the process exits immediately with code 0
/// without registering any Temporal services. When enabled, it registers the Temporal client
/// and a hosted worker for the session run workflow (see <see cref="WorkerHostConfiguration"/>).
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Runs the worker host.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code (0 when disabled or after clean shutdown).</returns>
    internal static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        if (!WorkerHostConfiguration.IsEnabled(builder.Configuration))
        {
            Console.WriteLine("Workflows:Enabled=false — worker host exiting (code 0).");
            return 0;
        }

        WorkerHostConfiguration.ConfigureServices(builder.Services, builder.Configuration);

        var host = builder.Build();
        await WorkerHostConfiguration.EnsureSessionSchemaAsync(host.Services, CancellationToken.None).ConfigureAwait(false);
        await host.RunAsync().ConfigureAwait(false);

        return 0;
    }
}
