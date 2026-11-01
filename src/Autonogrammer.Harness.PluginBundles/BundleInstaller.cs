using System.IO.Compression;
using System.Text.Json;

namespace Autonogrammer.Harness.PluginBundles;


/// <summary>Installs a folder or zip into a plugins root by extracting to a sibling temp directory and renaming. A failure deletes the temp directory and leaves no bundle folder.</summary>
public sealed class BundleInstaller
{
    /// <summary>The sidecar file name inside an installed bundle.</summary>
    public const string SidecarName = "install.json";

    private readonly Func<IReadOnlySet<string>>? _reservedSkills;
    private readonly Func<IReadOnlySet<string>>? _reservedCommands;

    /// <summary>Initializes a new instance of the <see cref="BundleInstaller"/> class.</summary>
    /// <param name="reservedSkills">Built-in skill ids, read at install time. Null means none.</param>
    /// <param name="reservedCommands">Built-in command ids, read at install time. Null means none.</param>
    public BundleInstaller(Func<IReadOnlySet<string>>? reservedSkills = null, Func<IReadOnlySet<string>>? reservedCommands = null)
    {
        _reservedSkills = reservedSkills;
        _reservedCommands = reservedCommands;
    }

    /// <summary>Installs a folder or a <c>.zip</c> into <paramref name="destinationRoot"/>.</summary>
    /// <param name="sourcePath">The folder or zip.</param>
    /// <param name="destinationRoot">The plugins root, created if needed.</param>
    /// <param name="scope">User or project scope, stored in the sidecar.</param>
    /// <returns>The installed bundle directory.</returns>
    /// <exception cref="BundleValidationException">The bundle was refused. No bundle directory remains.</exception>
    public string Install(string sourcePath, string destinationRoot, BundleInstallScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        Directory.CreateDirectory(destinationRoot);
        var temp = Path.Combine(destinationRoot, ".install-" + Guid.NewGuid().ToString("N"));
        string? committed = null;
        try
        {
            Directory.CreateDirectory(temp);
            string id;
            if (Directory.Exists(sourcePath))
            {
                id = BundleValidator.ValidateDirectory(sourcePath, Skills(), Commands());
                CopyDirectory(sourcePath, temp);
            }
            else
            {
                var fallback = Path.GetFileNameWithoutExtension(sourcePath);
                id = BundleValidator.ValidateZip(sourcePath, fallback);
                ExtractZip(sourcePath, temp);
                id = BundleValidator.ValidateDirectory(temp, Skills(), Commands());
            }

            WriteSidecar(temp, id, scope);
            var target = Path.Combine(destinationRoot, id);
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            Directory.Move(temp, target);
            committed = target;
            temp = string.Empty;
            return target;
        }
        catch
        {
            if (committed is not null && Directory.Exists(committed))
            {
                Directory.Delete(committed, recursive: true);
            }

            throw;
        }
        finally
        {
            if (temp.Length > 0 && Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    /// <summary>Reads the scope stored by <see cref="Install"/>.</summary>
    /// <param name="bundleDirectory">The installed bundle directory.</param>
    /// <returns>The scope, or <see cref="BundleInstallScope.User"/> when the sidecar is absent.</returns>
    public static BundleInstallScope ReadScope(string bundleDirectory)
    {
        var path = Path.Combine(bundleDirectory, SidecarName);
        if (!File.Exists(path))
        {
            return BundleInstallScope.User;
        }

        var sidecar = JsonSerializer.Deserialize(File.ReadAllText(path), BundleInstallJsonContext.Default.BundleInstallSidecar);
        return string.Equals(sidecar?.Scope, "project", StringComparison.Ordinal) ? BundleInstallScope.Project : BundleInstallScope.User;
    }

    private IReadOnlySet<string> Skills() => _reservedSkills?.Invoke() ?? new HashSet<string>(StringComparer.Ordinal);

    private IReadOnlySet<string> Commands() => _reservedCommands?.Invoke() ?? new HashSet<string>(StringComparer.Ordinal);

    private static void WriteSidecar(string directory, string id, BundleInstallScope scope)
    {
        var sidecar = new BundleInstallSidecar
        {
            Id = id,
            Scope = scope == BundleInstallScope.Project ? "project" : "user",
        };
        File.WriteAllText(
            Path.Combine(directory, SidecarName),
            JsonSerializer.Serialize(sidecar, BundleInstallJsonContext.Default.BundleInstallSidecar));
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(source))
        {
            var name = Path.GetFileName(path);
            var target = Path.Combine(destination, name);
            if (Directory.Exists(path))
            {
                Directory.CreateDirectory(target);
                CopyDirectory(path, target);
            }
            else
            {
                File.Copy(path, target, overwrite: false);
            }
        }
    }

    private static void ExtractZip(string zipPath, string destination)
    {
        var root = Path.GetFullPath(destination);
        using var archive = ZipFile.OpenRead(zipPath);
        long written = 0;
        var count = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (BundleValidator.IsSymlink(entry))
            {
                throw new BundleValidationException(BundleInvalidCode.BundleSymlink, entry.FullName);
            }

            var relative = entry.FullName.Replace('\\', '/');
            var target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !string.Equals(target, root, StringComparison.Ordinal))
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTraversal, relative);
            }

            count++;
            if (count > BundleValidator.MaxFileCount)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooManyFiles);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            int read;
            long file = 0;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                file += read;
                written += read;
                if (file > BundleValidator.MaxFileBytes)
                {
                    throw new BundleValidationException(BundleInvalidCode.BundleTooLarge, relative);
                }

                if (written > BundleValidator.MaxBundleBytes)
                {
                    throw new BundleValidationException(BundleInvalidCode.BundleTooBig);
                }

                output.Write(buffer, 0, read);
            }
        }
    }
}
