namespace OmpOS.Presentation.Settings;

/// <summary>The result of one diagnostics run.</summary>
/// <param name="Checks">One check per endpoint, in the order the source listed the endpoints.</param>
/// <param name="At">When the run finished.</param>
public sealed record NetworkDiagnosticsReport(IReadOnlyList<EndpointCheck> Checks, DateTimeOffset At);
