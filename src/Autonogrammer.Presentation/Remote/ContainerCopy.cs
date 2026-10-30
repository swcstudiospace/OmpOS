namespace Autonogrammer.Presentation.Remote;

/// <summary>The words of the container and WSL part of the environments page, the running-container picker and the New session picker. The container runtime is podman only.</summary>
public static class ContainerCopy
{
    /// <summary>The heading of the container and WSL section.</summary>
    public const string Title = "Containers and WSL";

    /// <summary>The explanation of what Autonogrammer does with containers and WSL, and what it never does.</summary>
    public const string Explanation =
        "A session can run inside a local podman container (a dev container or any running container) or inside a WSL distribution. "
        + "Autonogrammer only attaches: it never builds, starts or changes a container by itself, and it never stores a secret. Provider tokens reach omp on a private channel, never on a command line.";

    /// <summary>The label of the button that opens the picker of running containers.</summary>
    public const string AddFromRunningLabel = "Add a running container";

    /// <summary>The label of the button that starts a hand-entered container profile.</summary>
    public const string AddContainerLabel = "Add a container by name";

    /// <summary>The label of the button that starts a WSL profile.</summary>
    public const string AddWslLabel = "Add a WSL distribution";

    /// <summary>The label of the button that starts a stopped container.</summary>
    public const string StartContainerLabel = "Start container";

    /// <summary>The label of the button that refreshes the list of running containers.</summary>
    public const string RefreshLabel = "Refresh";

    /// <summary>The heading of the running-container picker.</summary>
    public const string PickerHeading = "Running containers";

    /// <summary>The accessible name of the running-container list.</summary>
    public const string PickerListName = "Running containers";

    /// <summary>The text shown while podman lists the containers.</summary>
    public const string PickerLoading = "Asking podman for the running containers.";

    /// <summary>The text shown when no container is running.</summary>
    public const string PickerEmpty = "No container is running. Start one with podman or the devcontainer CLI, then refresh. Autonogrammer does not start containers.";

    /// <summary>The label of a dev container in the picker.</summary>
    public const string DevContainerLabel = "dev container";

    /// <summary>The marker of a container that is already saved as an environment.</summary>
    public const string AlreadyAddedLabel = "already added";

    /// <summary>The heading of the container form when adding.</summary>
    public const string AddContainerHeading = "Add a container";

    /// <summary>The heading of the container form when editing.</summary>
    public const string EditContainerHeading = "Edit a container";

    /// <summary>The heading of the WSL form when adding.</summary>
    public const string AddWslHeading = "Add a WSL distribution";

    /// <summary>The heading of the WSL form when editing.</summary>
    public const string EditWslHeading = "Edit a WSL distribution";

    /// <summary>The label of the container kind in a list.</summary>
    public const string ContainerKindLabel = "Container";

    /// <summary>The label of the WSL kind in a list.</summary>
    public const string WslKindLabel = "WSL";

    /// <summary>The label of an option of a container in the picker, in front of the name.</summary>
    public const string PickerPrefix = "container: ";

    /// <summary>The label of an option of a WSL distribution in the picker, in front of the name.</summary>
    public const string WslPickerPrefix = "wsl: ";

    /// <summary>The label of the preview of the folder inside a container.</summary>
    public const string FolderInContainerPrefix = "Folder in the container: ";

    /// <summary>The label of the preview of the folder inside a WSL distribution.</summary>
    public const string FolderInDistroPrefix = "Folder in the distribution: ";

    /// <summary>The note on the WSL section on a system other than Windows.</summary>
    public const string WslWindowsOnly = "WSL environments run on Windows only. A saved WSL environment is kept but cannot be used on this computer.";

    /// <summary>The note that says what a container profile needs: a dev container workspace is usually under /workspaces.</summary>
    public const string WorkspaceFolderHint = "The folder inside the container that omp works in. A dev container usually mounts the project at /workspaces/<folder name>.";

    /// <summary>The note on a loopback MCP server that cannot reach a container or distribution.</summary>
    public const string LoopbackNote = "MCP servers on this computer's loopback address are not reachable from inside a container or distribution; start such a server inside it, or use one on a reachable address.";

    /// <summary>The label of the container name field.</summary>
    public const string ContainerNameLabel = "Container name or id";

    /// <summary>The label of the user field of a container.</summary>
    public const string ContainerUserLabel = "User inside the container (optional)";

    /// <summary>The label of the folder field of a container.</summary>
    public const string ContainerFolderLabel = "Folder inside the container";

    /// <summary>The label of the podman path field.</summary>
    public const string PodmanPathLabel = "podman executable (optional)";

    /// <summary>The help of the podman path field.</summary>
    public const string PodmanPathHelp = "Leave empty to use podman from PATH.";

    /// <summary>The label of the omp path field of a container or distribution.</summary>
    public const string OmpPathInsideLabel = "omp inside the environment (optional)";

    /// <summary>The help of the omp path field of a container or distribution.</summary>
    public const string OmpPathInsideHelp = "Leave empty to find omp on the PATH inside, ~/.bun/bin, ~/.local/bin and /usr/local/bin.";

    /// <summary>The label of the distribution name field.</summary>
    public const string DistroLabel = "WSL distribution";

    /// <summary>The label of the user field of a distribution.</summary>
    public const string DistroUserLabel = "User inside the distribution (optional)";

    /// <summary>The label of the folder field of a distribution.</summary>
    public const string DistroFolderLabel = "Folder inside the distribution";

    /// <summary>The accessible name of the list of the saved container and WSL environments.</summary>
    public const string EnvironmentsListName = "Container and WSL environments";

    /// <summary>The accessible name of the live region of the section.</summary>
    public const string AnnouncementName = "Containers and WSL status";

    /// <summary>The label of the button that adds the chosen running container.</summary>
    public const string AddChosenLabel = "Add the chosen container";

    /// <summary>The label of the button that closes the list of running containers.</summary>
    public const string ClosePickerLabel = "Close the list";

    /// <summary>The text of the section when no container or WSL environment is saved.</summary>
    public const string EmptyText = "No container or WSL environment is saved yet. Add a running container, or one by name.";
}
