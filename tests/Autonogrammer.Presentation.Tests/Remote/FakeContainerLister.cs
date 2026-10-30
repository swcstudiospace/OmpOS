using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Presentation.Tests;

/// <summary>An <see cref="IContainerLister"/> that returns what a test scripted.</summary>
internal sealed class FakeContainerLister : IContainerLister
{
    /// <summary>Gets or sets the listing returned.</summary>
    public ContainerListing Listing { get; set; } = new([], null);

    /// <summary>Gets or sets the exception thrown, or <see langword="null"/>.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets the number of listings requested.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public Task<ContainerListing> ListRunningAsync(CancellationToken cancellationToken)
    {
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        return Failure is { } failure ? Task.FromException<ContainerListing>(failure) : Task.FromResult(Listing);
    }
}
