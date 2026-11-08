namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>The three things a remote environment is provisioned with.</summary>
public enum ProvisionComponent
{
    /// <summary>The pinned Bun runtime under <c>~/.bun/bin/bun</c>.</summary>
    Bun,

    /// <summary>The latest omp release under <c>~/.local/bin/omp</c>.</summary>
    Omp,

    /// <summary>One bundled plugin directory at its pinned commit.</summary>
    Plugin,
}
