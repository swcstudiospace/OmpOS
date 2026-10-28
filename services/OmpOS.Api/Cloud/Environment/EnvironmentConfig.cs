namespace OmpOS.Api.Cloud;

/// <summary>
/// What a worker runs to prepare and start an agent's machine, read from <c>.ompos/environment.json</c>, Cursor's <c>.cursor/environment.json</c>,
/// or a saved environment definition. The control plane only reads and validates this; the worker runs <see cref="Install"/> after checkout and
/// <see cref="Start"/> at each wake.
/// </summary>
/// <param name="Snapshot">The name of a saved machine snapshot to start from, or null. Railway has no snapshots, so the worker ignores it and reports the gap.</param>
/// <param name="Install">The install command, run from the repository root after checkout. It must be idempotent. Null for none.</param>
/// <param name="Start">The command run after boot at every wake. Null for none.</param>
/// <param name="Terminals">The terminals to open after <see cref="Start"/>.</param>
/// <param name="Build">The Dockerfile build, or null. A Railway worker has no Docker daemon, so a build is parsed and reported as unsupported, not run.</param>
/// <param name="Source">Where this configuration came from: <c>personal</c>, <c>.ompos/environment.json</c> or <c>.cursor/environment.json</c>.</param>
internal sealed record EnvironmentConfig(
    string? Snapshot,
    string? Install,
    string? Start,
    IReadOnlyList<EnvironmentTerminal> Terminals,
    EnvironmentBuild? Build,
    string Source);
