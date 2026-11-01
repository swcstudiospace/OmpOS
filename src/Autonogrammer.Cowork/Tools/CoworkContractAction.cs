namespace Autonogrammer.Cowork.Tools;

/// <summary>Why the contract base is calling the provider.</summary>
public enum CoworkContractAction
{
    /// <summary>The call names a grant the provider must ask for.</summary>
    Grant = 0,

    /// <summary>The call is a sensitive action the provider must ask for.</summary>
    Sensitive = 1,
}
