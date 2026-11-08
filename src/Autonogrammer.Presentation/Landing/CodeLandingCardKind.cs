namespace Autonogrammer.Presentation.Landing;

/// <summary>The four environment cards of the Code landing, in the order they are shown and tabbed to.</summary>
public enum CodeLandingCardKind
{
    /// <summary>This computer.</summary>
    LocalMachine = 0,

    /// <summary>Another machine linked to the account.</summary>
    LinkedMachine = 1,

    /// <summary>Cloud agents.</summary>
    Cloud = 2,

    /// <summary>The saved remote environments.</summary>
    Remote = 3,
}
