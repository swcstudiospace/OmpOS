using OmpOS.Presentation.SideChats;

namespace OmpOS.Presentation.Tests.SideChats;

/// <summary>A forker that returns a scripted session and records the requests.</summary>
internal sealed class ScriptedSessionForker(bool canForkFromMessage) : ISessionForker
{
    /// <summary>Gets the requests it received.</summary>
    public List<ForkRequest> Requests { get; } = [];

    /// <summary>Gets or sets the exception the next fork throws.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public bool CanForkFromMessage => canForkFromMessage;

    /// <inheritdoc/>
    public Task<ForkResult> ForkAsync(ForkRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Failure is not null ? Task.FromException<ForkResult>(Failure) : Task.FromResult(new ForkResult("forked-" + Requests.Count, "Copy of chat"));
    }
}
