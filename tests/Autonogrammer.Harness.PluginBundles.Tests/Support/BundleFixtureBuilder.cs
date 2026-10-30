using System.IO.Compression;

namespace Autonogrammer.Harness.PluginBundles.Tests.Support;

/// <summary>Writes folder and zip fixtures into a test temp directory. No archive is stored in the repository.</summary>
internal static class BundleFixtureBuilder
{
    /// <summary>Writes a zip archive.</summary>
    /// <param name="directory">The directory that receives the archive.</param>
    /// <param name="fileName">The archive file name.</param>
    /// <param name="entries">The entries.</param>
    /// <returns>The archive path.</returns>
    public static string WriteZip(string directory, string fileName, IReadOnlyList<BundleZipEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(entries);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var entry in entries)
            {
                var zip = archive.CreateEntry(entry.Name, CompressionLevel.NoCompression);
                if (entry.Symlink)
                {
                    zip.ExternalAttributes = 0xA1FF << 16;
                }

                using var stream = zip.Open();
                if (entry.Content is byte[] bytes)
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    foreach (var part in entry.Content)
                    {
                        stream.WriteByte(part);
                    }
                }
            }
        }

        return path;
    }

    /// <summary>Writes a minimal valid bundle folder.</summary>
    /// <param name="directory">The bundle root to create.</param>
    /// <param name="skillName">The skill folder name.</param>
    public static void WriteFolder(string directory, string skillName = "foo")
    {
        Directory.CreateDirectory(Path.Combine(directory, "skills", skillName));
        File.WriteAllText(
            Path.Combine(directory, "skills", skillName, "SKILL.md"),
            "---\nname: " + skillName + "\ndescription: A fixture skill.\n---\n\n# " + skillName + "\n");
    }

    /// <summary>Builds bytes for a text entry.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Text(string text) => System.Text.Encoding.UTF8.GetBytes(text);
}
