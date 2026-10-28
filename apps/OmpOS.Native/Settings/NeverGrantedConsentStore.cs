using OmpOS.Workspace;

namespace OmpOS.Native.Settings;

/// <summary>
/// The setup approvals of a window that has no approval store: nothing is approved and nothing can be recorded. The Worktrees page then shows
/// every setup file as not approved, which is the safe answer.
/// </summary>
internal sealed class NeverGrantedConsentStore : ISetupConsentStore
{
    /// <summary>Gets the one instance.</summary>
    public static NeverGrantedConsentStore Instance { get; } = new();

    /// <inheritdoc/>
    public Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("This store holds no approvals and records none."));
}
