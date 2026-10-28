namespace OmpOS.Api.Cloud;

/// <summary>The outcome of parsing one environment file.</summary>
/// <param name="Config">The configuration, or null when the file has errors.</param>
/// <param name="Errors">Why the file was refused. Each names the member and never repeats a value.</param>
/// <param name="Warnings">What was ignored: unknown members, for example.</param>
internal sealed record EnvironmentConfigParseResult(EnvironmentConfig? Config, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
