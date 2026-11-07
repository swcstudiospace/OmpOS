namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Whether an install lands in the user plugins folder or a project folder.</summary>
public enum BundleInstallScope
{
    /// <summary>Installed under <c>~/.autonogrammer/plugins</c>. Enabled until the user turns it off.</summary>
    User = 0,

    /// <summary>Installed under a project <c>.autonogrammer/plugins</c> folder. Off until the user enables it.</summary>
    Project = 1,
}
