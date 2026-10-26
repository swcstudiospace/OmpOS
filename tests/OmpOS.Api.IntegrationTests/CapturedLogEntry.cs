using Microsoft.Extensions.Logging;

namespace OmpOS.Api.IntegrationTests;

/// <summary>One captured log entry and the log scope states that were active when it was written.</summary>
/// <param name="Category">The logger category.</param>
/// <param name="Level">The log level.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Scopes">The active scope states, outermost first.</param>
internal sealed record CapturedLogEntry(string Category, LogLevel Level, string Message, IReadOnlyList<object> Scopes);
