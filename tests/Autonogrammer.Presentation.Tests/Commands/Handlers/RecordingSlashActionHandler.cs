using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Presentation.Tests.Commands.Handlers;

/// <summary>A handler that returns a scripted result and records its requests.</summary>
internal sealed class RecordingSlashActionHandler(string actionId, SlashActionResult? result = null) : ISlashActionHandler
{
    /// <summary>Gets the requests it ran.</summary>
    public List<SlashActionRequest> Requests { get; } = [];

    /// <summary>Gets or sets whether it can run.</summary>
    public bool Ready { get; set; } = true;

    /// <summary>Gets or sets an exception it throws when run.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public string ActionId => actionId;

    /// <inheritdoc/>
    public bool CanRun => Ready;

    /// <inheritdoc/>
    public Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Failure is not null ? Task.FromException<SlashActionResult>(Failure) : Task.FromResult(result ?? SlashActionResult.Completed("done"));
    }
}
