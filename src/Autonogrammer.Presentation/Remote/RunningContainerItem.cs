using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Presentation.Remote;

/// <summary>One running container in the picker: its name, image, whether it is a dev container, and whether it is already saved as an environment.</summary>
public sealed class RunningContainerItem
{
    internal RunningContainerItem(ContainerSummary summary, bool alreadyAdded)
    {
        Summary = summary;
        AlreadyAdded = alreadyAdded;
    }

    /// <summary>Gets what podman listed.</summary>
    public ContainerSummary Summary { get; }

    /// <summary>Gets the container name: the label of the row.</summary>
    public string Name => Summary.Name;

    /// <summary>Gets whether the devcontainer CLI created the container (it carries the <c>devcontainer.local_folder</c> label).</summary>
    public bool IsDevContainer => Summary.DevContainerFolder is not null;

    /// <summary>Gets whether a saved environment already points at this container.</summary>
    public bool AlreadyAdded { get; }

    /// <summary>
    /// Gets the second line: the image, then <c>dev container</c> and <c>already added</c> in words (the markers never depend on colour or an icon alone).
    /// </summary>
    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (Summary.Image.Length > 0)
            {
                parts.Add(Summary.Image);
            }

            if (IsDevContainer)
            {
                parts.Add(ContainerCopy.DevContainerLabel);
            }

            if (AlreadyAdded)
            {
                parts.Add(ContainerCopy.AlreadyAddedLabel);
            }

            return string.Join(", ", parts);
        }
    }

    /// <summary>Gets the text a screen reader announces for the row: name and detail.</summary>
    public string AccessibleName => Detail.Length == 0 ? Name : Name + ". " + Detail;

    /// <summary>
    /// Gets the folder inside the container a dev container is expected to use: <c>/workspaces/&lt;project folder name&gt;</c>, the devcontainer default, or
    /// <see langword="null"/> for a container that is not a dev container (the user types the folder).
    /// </summary>
    public string? SuggestedWorkspaceFolder
    {
        get
        {
            if (Summary.DevContainerFolder is not { } folder)
            {
                return null;
            }

            var name = Path.GetFileName(folder.TrimEnd('/', '\\'));
            var candidate = "/workspaces/" + name;
            return name.Length > 0 && SshProfileRules.IsValidAbsoluteRemotePath(candidate) ? candidate : null;
        }
    }
}
