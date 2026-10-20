# Local development runbook

## Bootstrap

From a clean clone, one command does everything:

```sh
./bootstrap.sh
```

It installs the .NET SDK pinned in `global.json` into `~/.dotnet` when missing, then runs a locked restore, a warnings-as-errors Release build, the test suite, and a format verification. Any failing step exits non-zero.

Every shell that calls `dotnet` directly needs the user SDK first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
```

## Workflows profile

The Temporal dev server (needed only when `Workflows:Enabled` is true, Phase 3+) runs under podman-compose:

```sh
podman-compose --profile workflows up
```

This is the only supported invocation. The API, worker, and web runbooks land with their phases:

- API backend serving and SSE resume: added in Phase 2.
- Blazor web head serving: added in Phase 4.
- MAUI head builds: macOS CI only, never locally on Linux.

## Troubleshooting

- `error NU1004 ... pass an explicit --force-evaluate option`: the lock file drifted after a dependency change. Relock with `dotnet restore OmpOS.Linux.slnf --force-evaluate`. Note `CI=true` in the environment enables locked mode locally, so this also happens on the dev box.
- `error NU1510 ... will not be pruned`: a `PackageReference` names a package already in the shared framework (e.g. `System.Text.Json`). Remove the reference instead of pinning it.
- `error IDE0005` (unused using) or `error WHITESPACE`: run `dotnet format OmpOS.Linux.slnf` and commit the result.
- `podman-compose: failed to check if podman is installed` (or `command not found`): install podman, then `pipx install podman-compose==1.6.0`. Compose validation runs in CI; locally it is optional.
- `dotnet: command not found` after bootstrap: the current shell lacks the `DOTNET_ROOT` export above.
- Wrong SDK version (`dotnet --version` is not `10.0.402`): delete the stray SDK from `PATH` or unset a competing `DOTNET_ROOT`; `global.json` pins the exact version with `latestPatch` roll-forward.
