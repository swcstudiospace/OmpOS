namespace Autonogrammer.Presentation.Landing;

/// <summary>
/// Every text of the Code landing: the four environment cards that show when no session is selected. The views carry no literal; the
/// sentences that name a state are composed from these constants, and the words that come from other copy classes are referenced there.
/// </summary>
public static class CodeLandingCopy
{
    /// <summary>The heading above the cards.</summary>
    public const string Heading = "Start an agent";

    /// <summary>The title of the card for this computer.</summary>
    public const string LocalTitle = "Local machine";

    /// <summary>The title of the card for a machine linked to the account.</summary>
    public const string LinkedTitle = "Another machine linked to this account";

    /// <summary>The title of the card for cloud agents.</summary>
    public const string CloudTitle = "Cloud";

    /// <summary>The title of the card for the saved remote environments.</summary>
    public const string RemoteTitle = "Remote";

    /// <summary>The label of the button that tries a failed check again.</summary>
    public const string Retry = "Try again";

    /// <summary>The label of the button that signs in.</summary>
    public const string SignIn = "Sign in";

    /// <summary>The label of the button that opens the cloud agents page of Settings.</summary>
    public const string OpenCloud = "Open cloud agents";

    /// <summary>The Omp status of a card whose environment has not been checked.</summary>
    public const string OmpNotChecked = "Not checked yet";

    /// <summary>The Omp status while the local check runs.</summary>
    public const string OmpChecking = "Checking Omp.";

    /// <summary>The state message of this computer while the local check runs.</summary>
    public const string LocalChecking = "Checking Omp on this computer.";

    /// <summary>The state message of this computer when the check threw.</summary>
    public const string LocalFailed = "Omp could not be checked on this computer.";

    /// <summary>The state message of this computer when omp is runnable.</summary>
    public const string LocalReady = "Ready on this computer.";

    /// <summary>The state message of this computer when omp is not installed.</summary>
    public const string LocalNeedsOmp = "Install Omp to start an agent on this computer.";

    /// <summary>The state message of this computer when no check was made in this window.</summary>
    public const string LocalNotChecked = "Omp is not checked in this window.";

    /// <summary>The Omp status of a runnable install with a version: the version, then the source. Arguments: version, source.</summary>
    public const string OmpInstalledFormat = "Omp {0} from {1}.";

    /// <summary>The Omp status of a runnable install whose version could not be read. Argument: source.</summary>
    public const string OmpFoundFormat = "Omp found from {0}.";

    /// <summary>The Omp status when no omp is runnable. Argument: the installation guidance.</summary>
    public const string OmpNotInstalledFormat = "Omp is not installed. {0}";

    /// <summary>The Omp status when the check threw. Argument: the reason.</summary>
    public const string OmpCheckFailedFormat = "Omp could not be checked: {0}";

    /// <summary>The source of an omp found on PATH or in a known install location.</summary>
    public const string SourcePath = "PATH";

    /// <summary>The source of an omp at the configured absolute path.</summary>
    public const string SourceConfigured = "the configured path";

    /// <summary>The source of the managed install.</summary>
    public const string SourceManaged = "the managed install";

    /// <summary>The state message of the linked-machine card when the account is signed out.</summary>
    public const string LinkedSignedOut = "Sign in to link a machine";

    /// <summary>The state message of the linked-machine card when the account has no linked machines.</summary>
    public const string LinkedEmpty = "No linked machines yet";

    /// <summary>The state message of the linked-machine card when machines are listed. Starting an agent on one is not offered yet.</summary>
    public const string LinkedListed = "Starting an agent on a linked machine is not available yet.";

    /// <summary>The state message of the cloud card when the account is signed out.</summary>
    public const string CloudSignedOut = "Sign in to use cloud agents";

    /// <summary>The state message of the cloud card when the account is signed in and configured.</summary>
    public const string CloudReady = "Cloud agents are ready to use.";

    /// <summary>The state message of the remote card when environments are listed.</summary>
    public const string RemoteReady = "Choose a remote environment to start an agent on it.";

    /// <summary>The label of the button that opens Remote environments in Settings.</summary>
    public const string ManageRemote = "Manage remote environments";

    /// <summary>The name a screen reader announces for a next action: its label, then the card it belongs to. Arguments: label, card title.</summary>
    public const string ActionNameFormat = "{0}, {1}";
}
