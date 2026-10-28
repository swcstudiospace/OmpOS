namespace Autonogrammer.Harness.Completion.Tests;

/// <summary>One request the stub provider received.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="ContentType">The request content type.</param>
/// <param name="Body">The request body, decoded as text.</param>
internal sealed record StubRequest(string Method, string? ContentType, string Body);
