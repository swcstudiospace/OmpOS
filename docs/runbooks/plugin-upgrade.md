---
description: "How to upgrade one omp plugin pin and re-record its contract, what each contract test failure means, Realm's build and fail-closed specifics, and how to roll back."
---

# Plugin upgrades and the contract gate

Status: Built as an offline gate. The Live evidence check runs by hand with the plugin submodules initialised. It has not been run for Realm.

Last verified: 2026-11-17

Applies to: v0.18.0

## What the gate protects

Each built-in plugin's surface is recorded in `tests/Autonogrammer.Harness.AgentLoop.Tests/Fixtures/OmpPluginContracts/<plugin>.json` at the commit the superproject pins: the commands and tools it registers, the environment variables and schema identifiers it uses, its package identity, the omp extension entries it declares, and evidence literals that must appear in named source files at the pin. `OmpPluginContractTests` compares the recording with `OmpPluginCatalog` and with the gitlinks in the superproject index. An upgrade that renames or removes a name the app depends on therefore fails the build instead of a user's session.

The gate proves that the names and files the app relies on are the recorded ones. It does not prove that the plugin behaves correctly; the live checks in [plugins.md](../plugins.md#plugins-in-omp-terminals) do that.

## Upgrade one plugin

1. Check that the target commit exists on the sibling's remote: `git ls-remote <url> <sha>` must print it. Never pin a commit that exists only in a local clone.
2. From the repository root: `git -C plugins/<name> fetch origin`, `git -C plugins/<name> checkout <sha>`, then `git add plugins/<name>`.
3. Read the plugin's surface at the new pin and update its fixture: `pinnedCommit`, `packageVersion`, `licence`, `ompExtensions`, `commands`, `tools`, `environmentVariables`, `schemaIds` and `evidence`.
4. If `ExpectedCommands` or `ExpectedTools` must change, change `OmpPluginCatalog` as well. A change in what the app depends on is a code change, not only a pin change.
5. Run the offline gate: `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests --filter "Category!=Live"`. With the submodules initialised, run the evidence check: `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests --filter "Category=Live"`.
6. Commit the pin, the fixture and any catalog change together, and add a `CHANGELOG.md` entry.

## When a test fails

- **Pin mismatch** (`SubmodulePinsMatchTheGitlinksInTheIndex`): the fixture and the gitlink disagree. Update the fixture to the gitlink, or move the gitlink back.
- **Descriptor mismatch** (`RecordedCommandsAndToolsMatchTheCatalogDescriptors`): the plugin registers a different set of names. Either the app changes (descriptor, connection expectations and tests), or the pin stays where it was.
- **Missing evidence literal** (Live): the plugin moved the value the app reads. Find the new location, update the evidence, and confirm the observer or environment variable still works.
- **Manifest mismatch** (Live): the package version, licence or extension entry changed. Re-record it only after checking that the new extension entry still loads.

Do not weaken a test, skip it, or delete a fixture entry to make it pass.

## Realm

- Realm is an omp extension in package mode: `omp.extensions` points at `./dist/src/omp/extension.js`. Build it on the host with `npm ci && npm run build` inside `plugins/agent-realm` (Node 22.13 or newer). `dist/` is not in the repository, so an unbuilt Realm is not loaded.
- Its default policy is `fail-closed` (`src/core/policy.schema.json`). Under it the plugin removes the omp built-ins it can replace and refuses guarded calls with `REALM_NOT_ACTIVE` while its daemon is not active. Changing the policy changes what the agent can do; record that in [ADR-0012](../decisions/ADR-0012-agent-loop-plugins.md) before changing it.
- Realm's tool names are `exec`, `read`, `write`, `list` and `status` (`src/omp/tools.ts` at the pin).

## Roll back

Revert the superproject commit that moved the gitlink and the fixture, then run `git submodule update --init plugins/<name>` to check out the previous pin. Run the offline gate on the reverted tree.

## Related

- [Plugins](../plugins.md): where each plugin lives and the launch contract.
- [ADR-0012](../decisions/ADR-0012-agent-loop-plugins.md): accepted decisions and the 2026-11-17 update.
