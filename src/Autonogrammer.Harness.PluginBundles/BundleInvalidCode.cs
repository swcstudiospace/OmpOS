namespace Autonogrammer.Harness.PluginBundles;

/// <summary>The named reason a bundle was refused. The user-facing form is <c>BundleInvalid(code)</c>.</summary>
public enum BundleInvalidCode
{
    /// <summary>An entry path leaves the bundle directory.</summary>
    BundleTraversal = 0,

    /// <summary>An entry is a symbolic link.</summary>
    BundleSymlink = 1,

    /// <summary>One file is larger than 2 MiB.</summary>
    BundleTooLarge = 2,

    /// <summary>The bundle holds more than 2,000 files.</summary>
    BundleTooManyFiles = 3,

    /// <summary>The bundle is larger than 50 MiB.</summary>
    BundleTooBig = 4,

    /// <summary>The bundle id or a component name is not allowed.</summary>
    BundleBadName = 5,

    /// <summary>The optional plugin manifest is present but not readable.</summary>
    BundleBadManifest = 6,

    /// <summary>A bundle skill or command uses a built-in id.</summary>
    BundleNameCollision = 7,
}
