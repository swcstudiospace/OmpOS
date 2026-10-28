using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;

namespace OmpOS.Presentation.Tests;

/// <summary>An in-memory <see cref="IContainerEnvironmentsHost"/> that counts what the page asked of it and returns what a test scripted.</summary>
internal sealed class FakeContainerEnvironmentsHost : IContainerEnvironmentsHost
{
    /// <summary>Gets or sets whether WSL is supported.</summary>
    public bool WslSupported { get; set; } = true;

    /// <summary>Gets or sets what a container test returns.</summary>
    public RemotePreflightReport ContainerReport { get; set; } = new() { Succeeded = true, Diagnoses = [], OmpVersion = "18.12.6", Platform = "Linux x86_64" };

    /// <summary>Gets or sets what a WSL test returns.</summary>
    public RemotePreflightReport WslReport { get; set; } = new() { Succeeded = true, Diagnoses = [], OmpVersion = "18.12.6", Platform = "Linux x86_64" };

    /// <summary>Gets or sets the diagnosis a start returns, or <see langword="null"/> for success.</summary>
    public RemoteDiagnosis? StartProblem { get; set; }

    /// <summary>Gets or sets the exception a test or start throws, or <see langword="null"/>.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets or sets a task the test waits for before it answers, or <see langword="null"/> to answer at once.</summary>
    public Task? Gate { get; set; }

    /// <summary>Gets the containers tested.</summary>
    public List<ContainerProfile> ContainerTests { get; } = [];

    /// <summary>Gets the distributions tested.</summary>
    public List<WslProfile> WslTests { get; } = [];

    /// <summary>Gets the containers a start was requested for.</summary>
    public List<ContainerProfile> Starts { get; } = [];

    /// <inheritdoc />
    public bool SupportsWsl => WslSupported;

    /// <inheritdoc />
    public async Task<RemotePreflightReport> TestContainerAsync(ContainerProfile profile, CancellationToken cancellationToken)
    {
        ContainerTests.Add(profile);
        await WaitAsync(cancellationToken).ConfigureAwait(false);
        return ContainerReport;
    }

    /// <inheritdoc />
    public async Task<RemotePreflightReport> TestWslAsync(WslProfile profile, CancellationToken cancellationToken)
    {
        WslTests.Add(profile);
        await WaitAsync(cancellationToken).ConfigureAwait(false);
        return WslReport;
    }

    /// <inheritdoc />
    public async Task<RemoteDiagnosis?> StartContainerAsync(ContainerProfile profile, CancellationToken cancellationToken)
    {
        Starts.Add(profile);
        await WaitAsync(cancellationToken).ConfigureAwait(false);
        return StartProblem;
    }

    private async Task WaitAsync(CancellationToken cancellationToken)
    {
        if (Gate is { } gate)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }
    }
}
