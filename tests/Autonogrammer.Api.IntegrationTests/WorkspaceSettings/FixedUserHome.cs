using Autonogrammer.Api.Services.WorkspaceSettings;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>A home directory a test names, so no test reads the real home of the account that runs it.</summary>
internal sealed class FixedUserHome : IUserHomeProvider
{
    /// <summary>Initializes a new instance of the <see cref="FixedUserHome"/> class.</summary>
    /// <param name="home">The home directory, or null for a process with no home.</param>
    public FixedUserHome(string? home)
    {
        Home = home;
    }

    /// <inheritdoc />
    public string? Home { get; }
}
