using System.Text.Json;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Finds the container of a dev container project so a session can attach to it. It reads <c>.devcontainer/devcontainer.json</c> (or <c>.devcontainer.json</c>; JSON
/// with comments and trailing commas) for the name, the workspace folder, the user and how the container is made, then asks podman for the containers the
/// devcontainer CLI labelled with the project folder (<c>devcontainer.local_folder</c>). Autonogrammer never builds or starts a dev container: when none is running the
/// answer says to start it with the devcontainer CLI or podman, then attach.
/// </summary>
public sealed class DevContainerResolver
{
    /// <summary>The label the devcontainer CLI puts on a container with the absolute path of the project folder on the host.</summary>
    public const string LocalFolderLabel = "devcontainer.local_folder";

    private const long MaxDefinitionBytes = 1024 * 1024;
    private const string FolderBasenameVariable = "${localWorkspaceFolderBasename}";
    private const string FolderVariable = "${localWorkspaceFolder}";
    private const string WorkspacesRoot = "/workspaces/";
    private const int LegacyBuildKeyLength = 10;

    private static readonly string[] DefinitionPaths = [".devcontainer/devcontainer.json", ".devcontainer.json"];

    private readonly PodmanContainerLister _lister;

    /// <summary>Initializes a new instance of the <see cref="DevContainerResolver"/> class.</summary>
    /// <param name="lister">Asks podman for the containers of a folder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lister"/> is null.</exception>
    public DevContainerResolver(PodmanContainerLister lister)
    {
        ArgumentNullException.ThrowIfNull(lister);
        _lister = lister;
    }

    /// <summary>Reads the definition of a project folder.</summary>
    /// <param name="localFolder">The absolute path of the project folder on this machine.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The definition, or <see langword="null"/> when the folder has none.</returns>
    /// <exception cref="ArgumentException">The folder is not an absolute path.</exception>
    /// <exception cref="InvalidDataException">The definition file is too large or is not JSON (comments and trailing commas are allowed).</exception>
    public static async Task<DevContainerDefinition?> ReadDefinitionAsync(string localFolder, CancellationToken cancellationToken)
    {
        RequireFolder(localFolder);
        foreach (var relative in DefinitionPaths)
        {
            var path = Path.Combine(localFolder, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            if (new FileInfo(path).Length > MaxDefinitionBytes)
            {
                throw new InvalidDataException("The dev container definition is larger than 1 MiB.");
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            return Parse(path, localFolder, bytes);
        }

        return null;
    }

    /// <summary>Finds the container of a project folder.</summary>
    /// <param name="localFolder">The absolute path of the project folder on this machine.</param>
    /// <param name="cancellationToken">Cancels the lookup and stops podman.</param>
    /// <returns>What was found and what to do next. A problem is an outcome, not an exception.</returns>
    /// <exception cref="ArgumentException">The folder is not an absolute path without control characters.</exception>
    public async Task<DevContainerResolution> ResolveAsync(string localFolder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localFolder);
        RequireFolder(localFolder);
        DevContainerDefinition? definition;
        try
        {
            definition = await ReadDefinitionAsync(localFolder, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.InvalidDefinition,
                FixText = "The dev container definition could not be read (" + Describe(exception) + "). Fix .devcontainer/devcontainer.json, or add the container by name.",
            };
        }

        if (definition is null)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.NoDefinition,
                FixText = "This folder has no .devcontainer/devcontainer.json. Add the container by name from the list of running containers instead.",
            };
        }

        if (definition.WorkspaceFolder is null)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.InvalidDefinition,
                Definition = definition,
                FixText = "The workspace folder of the definition is missing or uses a variable Autonogrammer does not resolve. Set an absolute workspaceFolder in devcontainer.json, or add the container by name and type the folder.",
            };
        }

        var listing = await _lister.FindForFolderAsync(localFolder, cancellationToken).ConfigureAwait(false);
        if (listing.Problem is not null)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.RuntimeProblem,
                Definition = definition,
                Diagnosis = listing.Problem,
                FixText = listing.Problem.FixText,
            };
        }

        return Classify(definition, definition.WorkspaceFolder, TrimFolder(localFolder), listing.Containers);
    }

    private static DevContainerResolution Classify(DevContainerDefinition definition, string workspaceFolder, string folder, IReadOnlyList<ContainerSummary> candidates)
    {
        var containers = candidates
            .Where(container => container.DevContainerFolder is { } labelled && string.Equals(TrimFolder(labelled), folder, StringComparison.Ordinal))
            .ToList();
        var running = containers.Where(container => container.IsRunning).ToList();
        if (containers.Count == 0)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.NotCreated,
                Definition = definition,
                FixText = "No container exists for this project yet. Autonogrammer does not build or start dev containers: start it with the devcontainer CLI or podman, then attach.",
            };
        }

        if (running.Count == 0)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.NotRunning,
                Definition = definition,
                Containers = containers,
                FixText = "The dev container " + containers[0].Name + " is not running. Start it with 'podman start " + containers[0].Name + "' or the devcontainer CLI, then attach.",
            };
        }

        if (running.Count > 1)
        {
            return new DevContainerResolution
            {
                Outcome = DevContainerOutcome.Ambiguous,
                Definition = definition,
                Containers = containers,
                FixText = "More than one running container belongs to this project. Pick one from the list of running containers.",
            };
        }

        return new DevContainerResolution
        {
            Outcome = DevContainerOutcome.Found,
            Definition = definition,
            Containers = containers,
            Profile = new ContainerProfile
            {
                Container = running[0].Name,
                RemoteUser = definition.EffectiveUser is { } user && ContainerProfileRules.IsValidInnerUser(user) ? user : null,
                WorkspaceFolder = workspaceFolder,
            },
        };
    }

    private static void RequireFolder(string localFolder)
    {
        if (!SshProfileRules.IsValidLocalPath(localFolder) || !Path.IsPathFullyQualified(localFolder))
        {
            throw new ArgumentException("The folder must be an absolute path without control characters.", nameof(localFolder));
        }
    }

    private static string TrimFolder(string folder) =>
        folder.Length > 1 ? folder.TrimEnd('/', '\\') : folder;

    private static string Describe(Exception exception) =>
        exception is InvalidDataException ? exception.Message : "the file could not be read";

    private static DevContainerDefinition Parse(string path, string localFolder, byte[] bytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("the file is not valid JSON with comments: " + exception.Message, exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("the file does not hold a JSON object.");
            }

            var basename = Path.GetFileName(TrimFolder(localFolder));
            var explicitFolder = StringOf(root, "workspaceFolder");
            var workspaceFolder = explicitFolder is null
                ? (basename.Length > 0 ? WorkspacesRoot + basename : null)
                : Substitute(explicitFolder, basename, localFolder);
            return new DevContainerDefinition(
                path,
                StringOf(root, "name"),
                workspaceFolder is not null && SshProfileRules.IsValidAbsoluteRemotePath(workspaceFolder) ? workspaceFolder : null,
                StringOf(root, "remoteUser"),
                StringOf(root, "containerUser"),
                BuildKindOf(root),
                StringOf(root, "image"));
        }
    }

    // The two variables that name the project folder are the only ones a host can fill in without the devcontainer CLI.
    private static string? Substitute(string value, string basename, string localFolder)
    {
        var result = value
            .Replace(FolderBasenameVariable, basename, StringComparison.Ordinal)
            .Replace(FolderVariable, localFolder, StringComparison.Ordinal);
        return result.Contains("${", StringComparison.Ordinal) ? null : result;
    }

    private static string? StringOf(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) || text.Length > 1024 || text.Any(char.IsControl) ? null : text;
    }

    // The legacy build-file property (10 characters, starts with "d", ends in "File") and the compose-file property (ends in "File", mentions "Compose") are recognised by shape.
    private static DevContainerBuildKind BuildKindOf(JsonElement root)
    {
        var hasImage = false;
        var hasBuild = false;
        var hasCompose = false;
        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name;
            if (string.Equals(name, "image", StringComparison.Ordinal))
            {
                hasImage = true;
            }
            else if (string.Equals(name, "build", StringComparison.Ordinal))
            {
                hasBuild = true;
            }
            else if (name.EndsWith("File", StringComparison.Ordinal) && name.Contains("Compose", StringComparison.Ordinal))
            {
                hasCompose = true;
            }
            else if (name.Length == LegacyBuildKeyLength && name.EndsWith("File", StringComparison.Ordinal) && name.StartsWith('d'))
            {
                hasBuild = true;
            }
        }

        return hasCompose ? DevContainerBuildKind.Compose : hasBuild ? DevContainerBuildKind.Build : hasImage ? DevContainerBuildKind.Image : DevContainerBuildKind.None;
    }
}
