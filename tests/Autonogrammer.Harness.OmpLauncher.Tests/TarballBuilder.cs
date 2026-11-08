using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

/// <summary>Builds the tarballs and archives the tests serve, in memory.</summary>
internal static class TarballBuilder
{
    /// <summary>A valid omp package for <paramref name="version"/>: <c>package/package.json</c> and <c>package/dist/cli.js</c>.</summary>
    /// <param name="version">The version written into package.json.</param>
    /// <returns>The gzip-compressed tarball.</returns>
    public static byte[] Package(string version) => Build(tar =>
    {
        AddDirectory(tar, "package");
        AddFile(
            tar,
            "package/package.json",
            Encoding.UTF8.GetBytes("{\"name\":\"@oh-my-pi/pi-coding-agent\",\"version\":\"" + version + "\",\"bin\":{\"omp\":\"dist/cli.js\"}}"));
        AddDirectory(tar, "package/dist");
        AddFile(tar, "package/dist/cli.js", Encoding.UTF8.GetBytes("console.log('omp');\n"));
    });

    /// <summary>Writes entries into a new gzip-compressed tar.</summary>
    /// <param name="write">Adds the entries.</param>
    /// <returns>The compressed bytes.</returns>
    public static byte[] Build(Action<TarWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            using (var tar = new TarWriter(gzip, TarEntryFormat.Ustar, leaveOpen: true))
            {
                write(tar);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Adds a regular file.</summary>
    /// <param name="tar">The writer.</param>
    /// <param name="name">The entry name, exactly as written.</param>
    /// <param name="content">The file bytes.</param>
    public static void AddFile(TarWriter tar, string name, byte[] content)
    {
        using var data = new MemoryStream(content);
        tar.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
    }

    /// <summary>Adds a directory entry.</summary>
    /// <param name="tar">The writer.</param>
    /// <param name="name">The entry name.</param>
    public static void AddDirectory(TarWriter tar, string name) =>
        tar.WriteEntry(new UstarTarEntry(TarEntryType.Directory, name));

    /// <summary>Adds a symbolic link entry.</summary>
    /// <param name="tar">The writer.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="target">The link target.</param>
    public static void AddLink(TarWriter tar, string name, string target) =>
        tar.WriteEntry(new UstarTarEntry(TarEntryType.SymbolicLink, name) { LinkName = target });

    /// <summary>Builds a Bun-style zip with one executable entry.</summary>
    /// <param name="entryName">The entry path, such as <c>bun-linux-x64/bun</c>.</param>
    /// <param name="content">The entry bytes.</param>
    /// <returns>The zip bytes.</returns>
    public static byte[] BunArchive(string entryName, byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(entryName);
            using var stream = entry.Open();
            stream.Write(content);
        }

        return buffer.ToArray();
    }
}
