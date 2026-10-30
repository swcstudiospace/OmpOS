using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>An automations provider that lists fixed automations and counts the reads.</summary>
internal sealed class StubAutomationsProvider : IWorkspaceAutomationsProvider
{
    private readonly IReadOnlyList<WorkspaceAutomationInfo> _automations;

    /// <summary>Initializes a new instance of the <see cref="StubAutomationsProvider"/> class.</summary>
    /// <param name="automations">The automations every listing returns.</param>
    public StubAutomationsProvider(IReadOnlyList<WorkspaceAutomationInfo> automations)
    {
        _automations = automations;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkspaceAutomationInfo>?> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkspaceAutomationInfo>?>(_automations);
}
