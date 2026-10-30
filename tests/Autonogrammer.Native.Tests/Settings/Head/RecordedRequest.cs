namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>What a recording handler saw of one request, copied before the request is disposed.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The requested address.</param>
/// <param name="HeaderNames">The names of every header the request carried, including content headers.</param>
/// <param name="HasContent">True when the request had a body.</param>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyList<string> HeaderNames, bool HasContent);
