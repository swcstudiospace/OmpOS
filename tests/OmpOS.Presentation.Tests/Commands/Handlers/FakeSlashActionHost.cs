using OmpOS.Presentation.Commands;

namespace OmpOS.Presentation.Tests.Commands.Handlers;

/// <summary>A head host that runs only the ids it is given.</summary>
internal sealed class FakeSlashActionHost(params string[] ids) : ISlashActionHost
{
    /// <summary>Gets the requests it ran.</summary>
    public List<SlashActionRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public bool CanRun(string actionId) => ids.Contains(actionId, StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(SlashActionResult.Completed("head ran " + request.ActionId));
    }
}
