using System.Text.Json;

namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// Lists the models omp knows by running <c>omp models --json --no-extensions</c>. The listing is offline when omp's model cache
/// is warm; the very first run of a new agent directory may make omp refresh its catalog over the network. This type never runs
/// <c>omp models refresh</c> or <c>omp usage</c>.
/// </summary>
/// <remarks>
/// The listing is credential-gated by omp (PROBE-4): it contains only the models of providers omp holds a credential for, so an
/// agent directory without keys lists no model at all. <c>--no-extensions</c> is passed because <c>omp models</c> would otherwise
/// load every extension of the agent directory just to print a list.
/// </remarks>
public sealed class OmpModelCatalog
{
    private readonly OmpCommandRunner _runner;
    private readonly OmpConfigOptions _options;

    /// <summary>Creates a catalog reader.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public OmpModelCatalog(OmpConfigOptions options)
        : this(options, new OmpCommandRunner(options))
    {
    }

    /// <summary>Creates a catalog reader that shares a runner.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <param name="runner">The runner that starts omp.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OmpModelCatalog(OmpConfigOptions options, OmpCommandRunner runner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runner);
        _options = options;
        _runner = runner;
    }

    /// <summary>
    /// Lists the models. Failures (omp missing, timeout, unreadable output) come back as an incomplete listing with a warning,
    /// never as an exception.
    /// </summary>
    /// <param name="cancellationToken">Cancels the listing; a cancelled listing throws.</param>
    /// <returns>The listing.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpModelListing> ListAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            new OmpCommandRequest(["models", "--json", "--no-extensions"]),
            cancellationToken).ConfigureAwait(false);
        return Interpret(result, _options);
    }

    /// <summary>Turns one invocation result into a listing. Exposed to the library so other writers can reuse the same reading.</summary>
    /// <param name="result">The result of <c>omp models --json</c>.</param>
    /// <param name="options">The options used to hide paths in the warning.</param>
    /// <returns>The listing.</returns>
    internal static OmpModelListing Interpret(OmpCommandResult result, OmpConfigOptions options)
    {
        if (result.StartFailure is not null)
        {
            return Failed(result.StartFailure);
        }

        if (result.TimedOut)
        {
            return Failed("omp did not list its models within the time limit.");
        }

        var stderrLine = OmpOutputText.FirstLine(result.StandardError, options);
        if (result.ExitCode != 0)
        {
            return Failed(stderrLine ?? $"omp models exited with code {result.ExitCode}.");
        }

        if (result.OutputTruncated)
        {
            return Failed("omp printed more model data than the output limit allows.");
        }

        OmpModelsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(result.StandardOutput, OmpConfigJsonContext.Default.OmpModelsDocument);
        }
        catch (JsonException)
        {
            return Failed("omp printed a model list that is not valid JSON.");
        }

        if (document?.Models is null)
        {
            return Failed("omp printed no model list.");
        }

        var models = new List<OmpModelInfo>(document.Models.Count);
        foreach (var row in document.Models)
        {
            if (ToModel(row) is { } model)
            {
                models.Add(model);
            }
        }

        var complete = !OmpOutputText.ReportsValidationFailure(result.StandardError);
        return new OmpModelListing(models, stderrLine, complete);
    }

    private static OmpModelListing Failed(string warning) => new([], warning, IsComplete: false);

    private static OmpModelInfo? ToModel(OmpModelRow row)
    {
        if (string.IsNullOrEmpty(row.Provider) || string.IsNullOrEmpty(row.Id))
        {
            return null;
        }

        var selector = string.IsNullOrEmpty(row.Selector) ? $"{row.Provider}/{row.Id}" : row.Selector;
        return new OmpModelInfo(
            row.Provider,
            row.Id,
            selector,
            string.IsNullOrEmpty(row.Name) ? row.Id : row.Name,
            row.Kind ?? "chat",
            Clamp(row.ContextWindow),
            Clamp(row.MaxTokens),
            row.Reasoning ?? false,
            row.Thinking is null ? [] : [.. row.Thinking],
            row.Input is null ? [] : [.. row.Input],
            row.Cost is null ? null : new OmpModelCost(row.Cost.Input, row.Cost.Output, row.Cost.CacheRead, row.Cost.CacheWrite),
            row.PricingStatus);
    }

    private static int? Clamp(long? value) =>
        value is null ? null : (int)Math.Clamp(value.Value, 0, int.MaxValue);
}
