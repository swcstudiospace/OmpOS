using OmpOS.Presentation.SideChats;

namespace OmpOS.Native.Tests.SideChats;

/// <summary>A forker that returns a new session and records the requests, for the view tests.</summary>
internal sealed class ViewSessionForker : ISessionForker
{
    /// <summary>Gets the requests it received.</summary>
    public List<ForkRequest> Requests { get; } = [];

    /// <summary>Gets or sets the exception the next fork throws.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public bool CanForkFromMessage => false;

    /// <inheritdoc/>
    public Task<ForkResult> ForkAsync(ForkRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Failure is not null ? Task.FromException<ForkResult>(Failure) : Task.FromResult(new ForkResult("forked-" + Requests.Count, "Copy of chat"));
    }
}
