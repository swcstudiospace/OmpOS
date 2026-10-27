namespace OmpOS.Sandbox;

/// <summary>
/// One GraphQL error as Railway reports it.
/// </summary>
/// <param name="Message">The error message.</param>
/// <param name="Extensions">The error extensions, including the code.</param>
/// <param name="TraceId">The top-level trace identifier (UNVERIFIED: introspection needed for its location).</param>
internal sealed record RailwayGraphQlError(string? Message, RailwayWire.ErrorExtensions? Extensions, string? TraceId);
