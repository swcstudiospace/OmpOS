using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Executor;
using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>Wired Computer Use core.</summary>
public sealed class ComputerUseCore
{
    /// <summary>Gate port.</summary>
    public required IComputerActionGate Gate { get; init; }

    /// <summary>Concrete gate for host binding.</summary>
    public required ComputerActionGate ActionGate { get; init; }

    /// <summary>Executor. The only injector.</summary>
    public required ComputerActionExecutor Executor { get; init; }

    /// <summary>Grant registry port.</summary>
    public required IComputerGrantRegistry Grants { get; init; }

    /// <summary>Concrete grant store.</summary>
    public required GrantStore GrantStore { get; init; }

    /// <summary>Denylist port.</summary>
    public required IComputerDenylistStore Denylist { get; init; }

    /// <summary>Kill-switch port.</summary>
    public required IKillSwitchControl KillSwitch { get; init; }

    /// <summary>Concrete kill switch.</summary>
    public required KillSwitchCoordinator KillSwitchCoordinator { get; init; }

    /// <summary>Rate limiter.</summary>
    public required RateLimiter RateLimiter { get; init; }

    /// <summary>Catalogue.</summary>
    public required AppCatalogue Catalogue { get; init; }

    /// <summary>Validated options.</summary>
    public required ComputerUseOptions Options { get; init; }

    /// <summary>State directory supplied by the head.</summary>
    public required string StateDirectory { get; init; }
}
