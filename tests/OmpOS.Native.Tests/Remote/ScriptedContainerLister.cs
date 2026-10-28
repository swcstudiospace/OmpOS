using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;

namespace OmpOS.Native.Tests.Remote;

/// <summary>The lister of running containers, scripted: it returns the containers a test put in it.</summary>
internal sealed class ScriptedContainerLister : IContainerLister
{
    /// <summary>Gets or sets the containers it lists.</summary>
    public List<ContainerSummary> Containers { get; set; } = [];

    /// <summary>Gets or sets a problem to report instead of the containers, or null.</summary>
    public RemoteDiagnosis? Problem { get; set; }

    /// <summary>Gets the number of times the list was asked for.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc/>
    public Task<ContainerListing> ListRunningAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(new ContainerListing(Containers, Problem));
    }
}
