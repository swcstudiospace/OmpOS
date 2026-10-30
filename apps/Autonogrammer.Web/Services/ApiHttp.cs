namespace Autonogrammer.Web.Services;

/// <summary>The one <see cref="HttpClient"/> every API-facing service of a browser tab shares. Its base address is the API origin (or the page origin in Demo mode).</summary>
internal sealed class ApiHttp : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="ApiHttp"/> class.</summary>
    /// <param name="client">The client. This object owns it.</param>
    internal ApiHttp(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        Client = client;
    }

    /// <summary>Gets the client.</summary>
    internal HttpClient Client { get; }

    /// <inheritdoc />
    public void Dispose() => Client.Dispose();
}
