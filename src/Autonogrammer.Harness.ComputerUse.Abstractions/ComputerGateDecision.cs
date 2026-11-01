namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Gate result. Allowed carries a ticket; denied carries a stable code and message id.</summary>
public abstract record ComputerGateDecision
{
    private protected ComputerGateDecision()
    {
    }
}

