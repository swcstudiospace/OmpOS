namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>One request a <see cref="ScriptedHandler"/> received, captured before any response was produced.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The absolute request address.</param>
/// <param name="Authorization">The complete <c>Authorization</c> header value, or null.</param>
/// <param name="Accept">The <c>Accept</c> header value, or null.</param>
/// <param name="ContentType">The request <c>Content-Type</c>, or null when there was no body.</param>
/// <param name="Body">The request body text, empty when there was none.</param>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Accept, string? ContentType, string Body);
