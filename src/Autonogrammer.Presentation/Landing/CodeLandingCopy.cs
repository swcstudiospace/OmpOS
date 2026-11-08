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

    /// <summary>The state message of this computer when omp runs but the managed install is missing.</summary>
    public const string LocalMissing = "Ready on this computer. The managed Omp is not installed.";

    /// <summary>The state message of this computer while the managed install runs.</summary>
    public const string LocalInstalling = "Installing Omp on this computer.";

    /// <summary>The state message of this computer when the managed install did not complete.</summary>
    public const string LocalInstallFailed = "Omp could not be installed on this computer.";

    /// <summary>The label of the button that installs the managed omp.</summary>
    public const string InstallOmp = "Install Omp";

    /// <summary>The label of the button that cancels a running install.</summary>
    public const string CancelInstall = "Cancel";

    /// <summary>The progress while the release to install is read. It is announced as the install runs.</summary>
    public const string ProgressReadingRelease = "Reading the latest Omp version.";

    /// <summary>The progress while an install runs and its version is not yet known.</summary>
    public const string ProgressInstallingUnknown = "Installing Omp.";

    /// <summary>The progress while an install runs for a known version. Argument: version.</summary>
    public const string ProgressInstallingFormat = "Installing Omp {0}.";

    /// <summary>The Omp status when the managed install did not complete. Argument: the reason.</summary>
    public const string OmpInstallFailedFormat = "Omp could not be installed: {0}";

    /// <summary>The notice after an install or update made a newer omp the managed install. Argument: version.</summary>
    public const string UpdatedFormat = "Omp {0} is now the managed install.";

    /// <summary>The stale notice when the registry was reached before and the last check time is known. Argument: the time of the last check.</summary>
    public const string OfflineFormat = "Could not reach the registry to check for a newer Omp. Last checked {0}.";

    /// <summary>The stale notice when the registry has never been reached.</summary>
    public const string OfflineNeverChecked = "Could not reach the registry to check for a newer Omp.";

    /// <summary>The notice when a newer omp waits for the next launch because the omp in use cannot be replaced now.</summary>
    public const string UpdateDeferred = "A newer Omp will install on the next launch, because the current Omp is in use.";

    /// <summary>The notice when a newer omp failed its check and the previous one was restored. Argument: the reason.</summary>
    public const string RolledBackFormat = "A newer Omp failed its check and was rolled back. {0}";

    /// <summary>The notice when the update failed. Argument: the reason.</summary>
    public const string UpdateFailedFormat = "Omp could not be updated: {0}";

    /// <summary>The format of the time of the last successful check. Argument: the time in UTC.</summary>
    public const string LastCheckedFormat = "yyyy-MM-dd HH:mm 'UTC'";

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
