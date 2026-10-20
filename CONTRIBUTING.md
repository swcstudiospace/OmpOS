# Contributing to OmpOS

## Branches and commits

- Phase work lands through the GSD workflow; shipping owns the PR flow.
- Commit subjects use `{type}({phase}-{plan}): {description}`, e.g. `feat(01-03): architecture gates, CI, compose, docs skeleton`.
- Every commit body ends with a Linear trailer: `Refs SPE-xxxxx` (e.g. `Refs SPE-17754`). One commit may carry several trailers.
- Commit each task atomically: production code first, plan metadata (`SUMMARY.md`, `STATE.md`, `ROADMAP.md`) in a final docs commit.

## Formatting

- `.editorconfig` is the single rule source. Format before committing:

```sh
dotnet format OmpOS.Linux.slnf
```

- CI runs `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` and fails on drift.

## Lock files

- Every project commits a `packages.lock.json` (central package management with transitive pinning).
- `CI=true` in the environment enables locked restore locally, so any dependency change must relock:

```sh
dotnet restore OmpOS.Linux.slnf --force-evaluate
```

- Never commit with an inconsistent lock file: locked restore fails with `NU1004`.

## Adding a project

1. Create the project under `src/`, `services/`, `apps/`, or `tests/` with the matching SDK and TFM (`net10.0`).
2. Register it: `dotnet sln OmpOS.slnx add <path>` and append the path to `OmpOS.Linux.slnf` (skip the MAUI head there).
3. Add a one-line entry to the layer map in `tests/OmpOS.ArchitectureTests/LayerMap.cs`.
4. Add a `ProjectReference` from `tests/OmpOS.ArchitectureTests` so the assembly lands in the test output.
5. Restore with `--force-evaluate` and keep the whole suite green.

## TODO policy

- A `TODO` without an owner and an open-question link is refused in review.
- Form: `// TODO(owner): description — see docs/open-questions.md#OQ-NN`.
- A Phase 5 test enforces that every `TODO` links to an entry; until then the author checks it by hand.
