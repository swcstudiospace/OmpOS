# Phase 66 owner actions

Source: `docs/runbooks/rebrand-autonogrammer.md` section 5. The repository tool cannot perform these eight actions, so each one stays with the repository owner. Every item below is `Status: open`.

Each proving command shows the legacy name or the current state that the owner must change. While an item is open its command prints that state: a legacy match for items 1 to 5 and 7, the artwork blob ids for item 8 (compare them with the blob ids recorded before the artwork is replaced), and `exit=1` for item 6 when the relocated-frame fingerprint is absent. Legacy names are written in bracket-safe form (for example `Omp[O]S` matches the old spelling as a regular expression but does not contain it), so this file does not match the tool's rewrite pattern.

Nothing in this file records a completed external rename. Closing an item needs the owner's action in the external system and a re-run of its command showing the old state gone.

## 1. Repository, hosted project and domain names

Status: open

Rename the GitHub repository `swcstudiospace/Omp[O]S`, the Vercel project hosts (`omp[o]s-web*.vercel.app`), the Railway service and project names, and register the replacement for the `omp[o]s.dev` domain. Then run `apply --urls`, or re-run it after the next base move. GitHub redirects the old repository URL; Vercel and Railway do not. The AGPL source offer in `NOTICE`, `Directory.Build.props` `RepositoryUrl`, `README.md`, the `CHANGELOG.md` compare links, `.github/ISSUE_TEMPLATE/config.yml` and the repository-URL grep in `scripts/ci/check-licences.sh` all follow `--urls`.

Proving command:
```sh
git grep -n 'swcstudiospace/Omp[O]S' -- README.md Directory.Build.props .github/ISSUE_TEMPLATE/config.yml
```

## 2. Environment variables

Status: open

Rename the `OMP[O]S_` environment prefix to `AUTONOGRAMMER_` in Vercel project settings, Railway service variables, GitHub repository secrets and every developer `.env`. The list of new names is `git grep -ho 'AUTONOGRAMMER_[A-Z0-9_]*' | sort -u` after the tool has been applied.

Proving command:
```sh
git grep -n 'OMP[O]S_' -- docs README.md AGENTS.md
```

## 3. Local and hosted state carrying the old names

Status: open

Recreate or migrate every piece of state that still carries an old name: podman containers, volumes and secrets (`omp[o]s-postgres`, `omp[o]s-dragonfly`, `omp[o]s-greptimedb`, the worker image `omp[o]s-agent-worker`, the label `io.omp[o]s.cloud-agent`, the secret `omp[o]s-{id}-credential`), the PostgreSQL schema `omp[o]s` and its advisory-lock key, Dragonfly key prefixes, GreptimeDB queries on `omp[o]s.*` metrics, git branches `omp[o]s/cloud/*`, and the JWT issuer `omp[o]s-api` with audience `omp[o]s-worker` (deploy the API and the worker together). Hard cutover means no alias.

Proving command:
```sh
podman ps -a --format '{{.Names}}' | grep -E '^omp[o]s-'
podman volume ls --format '{{.Name}}' | grep -E '^omp[o]s-'
podman secret ls --format '{{.Name}}' | grep -E '^omp[o]s-'
git grep -n 'omp[o]s-api\|omp[o]s-worker' -- services tests
```

## 4. Android application identity

Status: open

Change the Android `ApplicationId` from `com.swcstudiospace.omp[o]s` to `com.swcstudiospace.autonogrammer`. This is a new store identity: installed apps will not upgrade.

Proving command:
```sh
git grep -n 'com.swcstudiospace.omp[o]s' -- apps/Autonogrammer.Android
```

## 5. Legal text

Status: open

The sentence in `NOTICE` that grants no right to use the name, and `docs/licensing.md`, carry the new name after the mechanical change. The owner confirms the wording.

Proving command:
```sh
git grep -n -e 'Omp[O]S' -e 'grants no right to use the name' -- NOTICE docs/licensing.md
```

## 6. Ignore-file fingerprints for relocated recorded frames

Status: open

After the rename commit exists, add `.gitleaksignore` fingerprints for the relocated recorded frames under `tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/` (`rpc-live-...ndjson` and similar). The old lines name history paths and stay untouched.

Proving command:
```sh
git grep -n 'Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-live' -- .gitleaksignore; echo "exit=$?"
```

## 7. Submodules

Status: open

The three submodules (`plugins/agent-substrate`, `plugins/agent-swarm`, `plugins/claude-ultrathink`) live in their own repositories. If they read the old environment prefix or the old `.omp[o]s` directory, change them in their own repositories before cutover, then advance the parent pins. Gate G9 of the rename inventory covers this item.

Proving command:
```sh
git submodule status -- plugins/agent-substrate plugins/agent-swarm plugins/claude-ultrathink
git grep --recurse-submodules -n 'OMP[O]S_' -- plugins/agent-substrate plugins/agent-swarm plugins/claude-ultrathink; echo "exit=$?"
```

## 8. Brand artwork and trademark decision

Status: open

New brand artwork and the trademark decision for `Autonogrammer` are design and legal tasks. The renamed PNG, ICO and ICNS files still carry the old pixels; the tool renames files and does not redraw them.

Proving command:
```sh
git ls-files -s -- apps/Autonogrammer.Native/Assets/Brand
git grep -n -i 'trademark' -- NOTICE docs/licensing.md README.md
```
