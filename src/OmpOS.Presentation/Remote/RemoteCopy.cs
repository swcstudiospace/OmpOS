namespace OmpOS.Presentation.Remote;

/// <summary>
/// Every text of the remote environments page and of the New session target picker, in one place so the views carry no literal. No sentence holds
/// a host name, a path or a credential: those come from the profiles. A state is always said in words, never by colour alone.
/// </summary>
public static class RemoteCopy
{
    /// <summary>The navigation label and id title of the Settings page.</summary>
    public const string Title = "Remote environments";

    /// <summary>The heading of the page.</summary>
    public const string Heading = "Remote environments";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "Run the agent on another machine over your own ssh. omp runs there, so its commands, files and tools are the remote machine's.";

    /// <summary>The explanation of what OmpOS does with ssh and what it never does.</summary>
    public const string Explanation =
        "OmpOS uses the ssh client and the ~/.ssh/config you already have. It never asks for or stores a password, a passphrase or a key; sign in with ssh-agent or an identity file. "
        + "It never changes known_hosts: a host you have not connected to before is confirmed by you in a terminal.";

    /// <summary>The text shown while the profiles load.</summary>
    public const string Loading = "Loading remote environments.";

    /// <summary>The heading of the empty state.</summary>
    public const string EmptyHeading = "No remote environments yet";

    /// <summary>The text of the empty state.</summary>
    public const string EmptyText = "Add a host from your ssh config, or enter one by hand. Nothing connects until you press Test connection or start a session on it.";

    /// <summary>The text shown when the profiles could not be loaded.</summary>
    public const string LoadFailed = "The remote environments could not be loaded.";

    /// <summary>The label of the button that retries a failed load.</summary>
    public const string RetryLabel = "Try again";

    /// <summary>The heading of the list of ssh config hosts.</summary>
    public const string CandidatesHeading = "Hosts in your ssh config";

    /// <summary>The text under the host list when it is empty.</summary>
    public const string NoCandidates = "No host is listed in your ssh config, or every one of them is already added.";

    /// <summary>The label of the button that adds a host from the ssh config.</summary>
    public const string AddFromConfigLabel = "Add";

    /// <summary>The label of the button that starts a hand-entered profile.</summary>
    public const string AddManualLabel = "Add a host by hand";

    /// <summary>The label of the edit button of a row.</summary>
    public const string EditLabel = "Edit";

    /// <summary>The label of the delete button of a row.</summary>
    public const string DeleteLabel = "Delete";

    /// <summary>The label of the connection test button.</summary>
    public const string TestLabel = "Test connection";

    /// <summary>The label of the button that stops a connection test.</summary>
    public const string CancelTestLabel = "Cancel test";

    /// <summary>The label of the button that opens the host trust terminal.</summary>
    public const string TrustHostLabel = "Trust host in a terminal";

    /// <summary>The explanation of the host trust terminal.</summary>
    public const string TrustHostHelp =
        "A terminal tab opens and runs ssh. Compare the fingerprint it shows with the one your administrator published, type yes only if they match, then press Test connection again. "
        + "OmpOS never accepts a host key for you.";

    /// <summary>The announcement after the host trust terminal was opened.</summary>
    public const string TrustHostOpened = "The host trust terminal is open. Answer the question in it, then test the connection again.";

    /// <summary>The announcement when the host trust terminal could not be opened.</summary>
    public const string TrustHostUnavailable = "No terminal is available here. Run ssh to the host once in your own terminal, then test the connection again.";

    /// <summary>The label of the save button of the editor.</summary>
    public const string SaveLabel = "Save";

    /// <summary>The label of the cancel button of the editor.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The heading of the editor when a host is added.</summary>
    public const string AddHeading = "Add a remote environment";

    /// <summary>The heading of the editor when a host is edited.</summary>
    public const string EditHeading = "Edit remote environment";

    /// <summary>The label of the display name field.</summary>
    public const string DisplayNameLabel = "Name";

    /// <summary>The label of the host alias field.</summary>
    public const string AliasLabel = "Host alias";

    /// <summary>The help of the host alias field.</summary>
    public const string AliasHelp = "A Host name from ~/.ssh/config, or a label when you give the host name below.";

    /// <summary>The label of the host name field.</summary>
    public const string HostnameLabel = "Host name or address (optional)";

    /// <summary>The label of the port field.</summary>
    public const string PortLabel = "Port (optional)";

    /// <summary>The label of the user field.</summary>
    public const string UserLabel = "User (optional)";

    /// <summary>The label of the identity file field.</summary>
    public const string IdentityFileLabel = "Identity file path (optional)";

    /// <summary>The help of the identity file field.</summary>
    public const string IdentityFileHelp = "A path to a private key file on this machine. OmpOS never reads it: ssh does.";

    /// <summary>The label of the jump host field.</summary>
    public const string ProxyJumpLabel = "Jump hosts (optional)";

    /// <summary>The label of the remote workspace root field.</summary>
    public const string RootLabel = "Remote workspace folder";

    /// <summary>The help of the remote workspace root field.</summary>
    public const string RootHelp = "An absolute path on the remote machine, for example /home/you/project. omp starts in it.";

    /// <summary>The label of the remote omp path field.</summary>
    public const string OmpPathLabel = "Remote omp path (optional)";

    /// <summary>The help of the remote omp path field.</summary>
    public const string OmpPathHelp = "Leave empty to find omp on the remote PATH, ~/.bun/bin, ~/.local/bin and /usr/local/bin.";

    /// <summary>The label of the agent forwarding switch.</summary>
    public const string ForwardAgentLabel = "Forward my ssh agent";

    /// <summary>The warning under the agent forwarding switch.</summary>
    public const string ForwardAgentWarning = "Anyone with root on the remote machine can use a forwarded agent as you. Leave it off unless the remote git needs it.";

    /// <summary>The label of the keep-alive field.</summary>
    public const string KeepAliveLabel = "Keep-alive interval in seconds";

    /// <summary>The label of the field issues list.</summary>
    public const string IssuesLabel = "Problems with this environment";

    /// <summary>The label that prefixes the last-used line.</summary>
    public const string LastUsedPrefix = "Last used ";

    /// <summary>The line for an environment no session has used.</summary>
    public const string NeverUsed = "Not used yet";

    /// <summary>The label of the diagnoses list.</summary>
    public const string DiagnosesLabel = "Findings";

    /// <summary>The prefix of an error diagnosis.</summary>
    public const string ErrorLabel = "Error";

    /// <summary>The prefix of a warning diagnosis.</summary>
    public const string WarningLabel = "Warning";

    /// <summary>The label of the how-to-fix line of a diagnosis.</summary>
    public const string FixLabel = "How to fix";

    /// <summary>The accessible name of the polite live region.</summary>
    public const string AnnouncementName = "Remote environments status";

    /// <summary>The heading of the target picker of the New session dialog.</summary>
    public const string PickerHeading = "Where should the agent run?";

    /// <summary>The accessible name of the target list.</summary>
    public const string PickerListName = "Run location";

    /// <summary>The label of the local option.</summary>
    public const string LocalLabel = "This computer";

    /// <summary>The description of the local option.</summary>
    public const string LocalDetail = "The agent runs here, in the folder you opened.";

    /// <summary>The label of the cloud option.</summary>
    public const string CloudLabel = "Cloud";

    /// <summary>The description of the cloud option.</summary>
    public const string CloudDetail = "A cloud agent on its own machine.";

    /// <summary>Why the cloud option cannot be chosen yet.</summary>
    public const string CloudUnavailable = "Cloud agents are not available in this build.";

    /// <summary>The message of a valid choice.</summary>
    public const string TargetOk = "Ready to start.";

    /// <summary>The message when nothing is chosen.</summary>
    public const string TargetNone = "Choose where the agent runs.";

    /// <summary>The message when a remote environment has never been tested.</summary>
    public const string TargetUntested = "This environment has not been tested yet. Starting will connect to it.";

    /// <summary>The prefix of the message when a remote environment's last test failed.</summary>
    public const string TargetBlockedPrefix = "Cannot start here: ";

    /// <summary>The label of the link from the picker to the settings page.</summary>
    public const string ManageLabel = "Manage remote environments";

    /// <summary>The label of the preview of the remote folder.</summary>
    public const string RemoteFolderPrefix = "Folder on the host: ";

    /// <summary>The label of the session badge when a turn was interrupted.</summary>
    public const string InterruptedBadge = "Interrupted";

    /// <summary>The label of an Ssh option in the picker, in front of the host.</summary>
    public const string SshPrefix = "ssh: ";
}
