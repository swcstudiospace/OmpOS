using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Write-ahead audit. A failed write is <see cref="DenialCode.AuditUnavailable"/>.</summary>
public interface IComputerUseAuditSink
{
    /// <summary>Writes the record before injection.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns><see langword="false"/> when the write failed.</returns>
    ValueTask<bool> TryWriteAheadAsync(ComputerActionRequest request, CancellationToken cancellationToken);
}
