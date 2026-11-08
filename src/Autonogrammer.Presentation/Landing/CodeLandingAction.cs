namespace Autonogrammer.Presentation.Landing;

/// <summary>What a button of the Code landing asks the window to do.</summary>
public enum CodeLandingAction
{
    /// <summary>Start a new agent on the environment named by the option identifier (<see cref="CodeLandingButton.OptionId"/>).</summary>
    StartOnEnvironment = 0,

    /// <summary>Check omp on this computer again.</summary>
    RetryOmpCheck = 1,

    /// <summary>Load the saved remote environments again.</summary>
    RetryRemoteLoad = 2,

    /// <summary>Open the Remote environments page of Settings.</summary>
    ManageRemote = 3,

    /// <summary>Open the Cloud page of Settings.</summary>
    OpenCloud = 4,

    /// <summary>Open the sign-in window.</summary>
    SignIn = 5,

    /// <summary>Install the managed omp on this computer. Also the retry after an install that did not complete.</summary>
    InstallOmp = 6,

    /// <summary>Cancel the managed install that is running.</summary>
    CancelOmpInstall = 7,
}
