using Autonogrammer.Presentation.Settings.Connectors;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>
/// A check source for fixtures that list no servers. Nothing in them calls a check, so it answers as a failed check if one is ever made.
/// </summary>
internal sealed class FakeConnectorCheckSource : IConnectorCheckSource
{
    public Task<ConnectorCheckSnapshot> CheckAsync(string serverId, CancellationToken cancellationToken) =>
        Task.FromResult(new ConnectorCheckSnapshot("Failed", []));
}
