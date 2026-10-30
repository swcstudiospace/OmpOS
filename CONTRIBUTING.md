# Contributing to Autonogrammer

Thank you for helping. Autonogrammer is licensed under the GNU Affero General Public License v3.0 or later (`AGPL-3.0-or-later`). Everything below applies to every contribution.

Community files: [Code of Conduct](CODE_OF_CONDUCT.md) · [Security policy](SECURITY.md) · [Support](SUPPORT.md) · [Pull request template](.github/PULL_REQUEST_TEMPLATE.md) · [Changelog](CHANGELOG.md).

## Ground rules

- Be kind: the [Code of Conduct](CODE_OF_CONDUCT.md) applies in every project space.
- Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md), never in a public issue.
- Never put secrets (keys, tokens, `.env` contents) in an issue, a pull request, a test or a fixture. Use environment variable names only.
- Ask first for large changes: open an issue or a discussion before you spend a week on a pull request.
- Container tooling is podman only. The word for the other tool does not appear in code, scripts or docs, and `BannedSymbols` enforces it.

## Licence of your contribution and the DCO sign-off

Autonogrammer is distributed under `AGPL-3.0-or-later`. By submitting a contribution you state that you wrote it or have the right to submit it, and you agree that it is licensed to the project and its users under `AGPL-3.0-or-later`, the same licence as the rest of the repository.

Every commit must carry a `Signed-off-by` line, the [Developer Certificate of Origin 1.1](https://developercertificate.org/) sign-off. It certifies the statements in that text for the commit. Add it with `-s`:

```sh
git commit -s -m "fix(web): keep the skip link first in the tab order"
# produces the trailer:
# Signed-off-by: Your Name <you@example.com>
```

- The name and e-mail address must match your Git identity (`git config user.name`, `git config user.email`), and the name must be your real name or a name you are known by.
- Forgot it on the last commit: `git commit --amend -s --no-edit`. On several commits: `git rebase --signoff main`, then `git push --force-with-lease` to your own branch.
- Maintainers sign off their own commits the same way.

## Build and gate commands

Prerequisites: the .NET SDK pinned in `global.json` (`./bootstrap.sh` installs it when missing, checking its SHA-512), `git`, `podman` (for the throwaway test services), Node.js and a Chrome (for the web smoke), `python3`. Clone with `--recurse-submodules`.

| Goal | Command |
| --- | --- |
| The whole gate: secret scan, plugin tests, locked restore, warnings-as-errors build, tests, format check, web smoke with axe | `./bootstrap.sh` |
| Locked restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` |
| Build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` |
| Tests (needs the test services of `scripts/test-services.sh`, which the gate starts on a pair of containers of its own; set `AUTONOGRAMMER_TEST_SERVICES_NAME` to give any other run its own pair when sessions share a host) | `dotnet test Autonogrammer.Linux.slnf -c Release --no-build` |
| Format (the single rule source is `.editorconfig`) | `dotnet format Autonogrammer.Linux.slnf` |
| Changelog check | `bash scripts/ci/check-changelog.sh` |

A pull request is ready when `./bootstrap.sh` exits 0 on your branch. CI (`.github/workflows/ci.yml`) runs the same steps plus the vulnerable-package audit and the compose validation. The CI runs on the project's self-hosted runner pool; pull requests from forks are not run there, so a maintainer runs the gate for you.

## Code expectations

- Warnings are errors, `AnalysisLevel` is `latest-all`, and there are no `#pragma warning disable`, `[SuppressMessage]` or `.editorconfig` severity edits: fix the cause.
- **One type per file** (Meziantou MA0048), sealed classes by default, file-scoped namespaces, XML documentation on public and internal members, `ArgumentNullException.ThrowIfNull` on parameters.
- Library code uses `ConfigureAwait(false)`; tests use `.ConfigureAwait(true)` and `TestContext.Current.CancellationToken`. Explicit culture and ordinal comparers on strings. JSON uses source-generated contexts.
- Tests are deterministic: inject `TimeProvider`, no sleeps, no network, no real `omp` in normal tests. A test that needs a real `omp` or `ssh` is a `[Fact]` with `Trait("Category", "Live")` behind an environment variable.
- Desktop AXAML: compiled bindings with `x:DataType`, tokens through `{DynamicResource Autonogrammer.*}`, no literal colours or font families, an `AutomationProperties.Name` on every interactive control. Web: no inline script or style, WCAG 2.2 AA, a visible focus ring, reduced motion honoured.
- Layering is enforced by `tests/Autonogrammer.ArchitectureTests`: apps, then services, then libraries, then contracts.
- Add or update tests for behaviour you change, and never weaken an existing test.

## Pull request checklist

The [pull request template](.github/PULL_REQUEST_TEMPLATE.md) carries this list:

1. The gate is green (`./bootstrap.sh`), formatting is clean and lock files are relocked when dependencies changed.
2. Tests cover the change.
3. Documentation is updated (README, `docs/`, and `CHANGELOG.md` under `[Unreleased]` for user-visible changes).
4. Every commit is signed off (DCO).
5. Open questions: new decisions or blockers have a row in `docs/open-questions.md`.
6. Screenshots (light and dark) for UI changes.
7. No secrets, and no new `TODO` without an owner and an open-question link.

## Branches and commits

- Phase work lands through the GSD workflow; shipping owns the PR flow.
- Commit subjects use `{type}({phase}-{plan}): {description}`, e.g. `feat(01-03): architecture gates, CI, compose, docs skeleton`. Outside contributors can use the same shape with the nearest phase number or `fix(web): ...` without one.
- Maintainers only: every commit body ends with a Linear trailer `Refs SPE-xxxxx` (e.g. `Refs SPE-17754`); one commit may carry several trailers. Outside contributors omit it.
- Commit each task atomically: production code first, plan metadata (`SUMMARY.md`, `STATE.md`, `ROADMAP.md`) in a final docs commit (maintainers).

## Formatting

- `.editorconfig` is the single rule source. Format before committing:

```sh
dotnet format Autonogrammer.Linux.slnf
```

- CI runs `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` and fails on drift.

## Lock files

- Every project commits a `packages.lock.json` (central package management with transitive pinning).
- `CI=true` in the environment enables locked restore locally, so any dependency change must relock. The command below relocks even when `CI=true` is set:

```sh
dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false
```

- Never commit with an inconsistent lock file: locked restore fails with `NU1004`.
- Dependabot pull requests may not regenerate `packages.lock.json`; a maintainer relocks them with the command above before merging.

## Adding a project

1. Create the project under `src/`, `services/`, `apps/`, or `tests/` with the matching SDK and TFM (`net10.0`).
2. Register it: `dotnet sln Autonogrammer.slnx add <path>` and append the path to `Autonogrammer.Linux.slnf` (skip the Android head there: the Linux filter excludes it).
3. Add a one-line entry to the layer map in `tests/Autonogrammer.ArchitectureTests/LayerMap.cs`.
4. Add a `ProjectReference` from `tests/Autonogrammer.ArchitectureTests` so the assembly lands in the test output.
5. Restore with `--force-evaluate` (and `-p:RestoreLockedMode=false` when `CI=true`) and keep the whole suite green.

## Plugin submodules

- `plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate` are git submodules pinned to commits on the sibling repositories. Clone with `--recurse-submodules` or run `git submodule update --init --recursive`.
- `plugins/agent-substrate` is a private repository: without read access it stays empty, the gate treats that as a warning, and the substrate plugin reports Unavailable. Outside contributors do not need it.
- Never edit sibling source inside Autonogrammer. Change it in the sibling repository, push it, then bump the pointer here.
- Never pin a commit that is not on the sibling's remote: check `git ls-remote <url> <sha>` before committing the bump.
- `.gitmodules` uses remote URLs, never local paths.
- `plugins/gsd-core-omp` is tracked Autonogrammer code, not a submodule.
- Full procedure, private-repository access and the CI secret: [docs/plugins.md](docs/plugins.md).

## TODO policy

- A `TODO` without an owner and an open-question link is refused in review.
- Form: `// TODO(owner): description — see docs/open-questions.md#OQ-NN`.
- A Phase 5 test enforces that every `TODO` links to an entry; until then the author checks it by hand.
