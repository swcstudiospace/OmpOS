namespace OmpOS.Presentation.Settings;

/// <summary>The outcome of the one request a diagnostics run sends to an endpoint.</summary>
/// <param name="Name">The provider the endpoint belongs to, as the user knows it.</param>
/// <param name="Host">The endpoint as host or host:port. It never holds a scheme, a path, a query or credentials; the page drops a value that does.</param>
/// <param name="Status">What the request found out.</param>
/// <param name="HttpStatus">The HTTP status code the endpoint answered with, or null when it did not answer.</param>
/// <param name="Latency">How long the request took, or null when it was not sent.</param>
/// <param name="Message">One plain sentence that says what happened. It holds no address and no credential.</param>
public sealed record EndpointCheck(string Name, string Host, EndpointStatus Status, int? HttpStatus, TimeSpan? Latency, string Message);
