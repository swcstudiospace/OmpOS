namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Opens a local port only after a card is answered.</summary>
public interface ILocalPortPermit
{
    /// <summary>Opens one host and port.</summary>
    /// <param name="host">The host.</param>
    /// <param name="port">The port.</param>
    void Open(string host, int port);
}
