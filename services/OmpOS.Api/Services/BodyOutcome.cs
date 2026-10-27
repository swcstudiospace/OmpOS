using Microsoft.AspNetCore.Http.HttpResults;

namespace OmpOS.Api.Services;

/// <summary>The outcome of reading a JSON request body: the value, or the problem that rejected it.</summary>
/// <typeparam name="T">The body type.</typeparam>
/// <param name="Value">The parsed value, or null when rejected.</param>
/// <param name="Error">The problem that rejected the body, or null when accepted.</param>
internal sealed record BodyOutcome<T>(T? Value, ProblemHttpResult? Error)
    where T : class;
