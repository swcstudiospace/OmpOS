using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Executes the verified commands of the operations it claims. The desktop calls a handler only after verification, dedupe and policy.</summary>
public interface IDispatchCommandHandler
{
    /// <summary>Reports whether this handler executes <paramref name="op"/>.</summary>
    /// <param name="op">The operation.</param>
    /// <returns>True when the handler claims the operation.</returns>
    bool CanHandle(DispatchOp op);

    /// <summary>Executes one command.</summary>
    /// <param name="command">The verified command.</param>
    /// <param name="cancellationToken">Cancelled when the device is revoked or the connection ends.</param>
    /// <returns>The terminal outcome.</returns>
    ValueTask<DispatchOutcome> HandleAsync(DispatchCommand command, CancellationToken cancellationToken);
}
