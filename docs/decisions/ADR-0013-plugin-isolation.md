# ADR-0013: Plugin isolation boundary

- Status: Proposed 2026-10-26. It records the boundary that `src/OmpOS.Harness.Plugins` implements today and the boundary ADR-0012 sets for omp plugins. Owner sign-off on the boundary text is pending (see Owner decisions needed).
- Date: 2026-10-26
- Owner: ompos-maintainers
- Deciders: ompos-maintainers
- Scope: where plugin code is allowed to run in OmpOS, what the host checks before it runs, and how a misbehaving plugin is stopped. Covers three kinds of extension: omp plugins (ADR-0012), declarative harness plugins with hooks (`src/OmpOS.Harness.Plugins`) and the in-process UI contributions (`src/OmpOS.Presentation/Plugins`, `src/OmpOS.Contracts/Plugins`). Does not cover MCP servers (ADR-0014), the contract version rules (ADR-0008) or how the four omp plugins are obtained and launched (ADR-0012).

## Context

[ADR-0004](ADR-0004-cross-platform-ui.md) decision 5 says ADE capabilities arrive through a plugin harness: in-process plugins first, MCP for out-of-process tools later. [ADR-0008](ADR-0008-contract-versioning.md) versions the contracts those plugins compile against. [ADR-0012](ADR-0012-agent-loop-plugins.md) adds four omp plugins that run inside `omp acp`. These are three different trust situations that were never compared in one place.

Facts read in the repository on 2026-10-26:

- No code in `src/` loads a .NET plugin assembly: a search of `src/` for `AssemblyLoadContext`, `Assembly.Load` and `LoadFrom` found nothing. The in-process contributions are objects registered by the host's own composition (`src/OmpOS.Presentation/Plugins/PluginHost.cs`, `RegisterPlugin`, `RegisterCommand`).
- A harness plugin manifest is JSON and carries no code (`PluginManifestParser`: size limit 64 KiB, JSON depth 16, unknown and repeated field names rejected, at most 32 hooks, at most 64 command arguments). `DeclarativePluginActivator` has nothing to start at activation.
- A hook is a command: an argument list and an absolute working directory. `SystemHookProcessRunner` starts it with `ProcessStartInfo.ArgumentList`, `UseShellExecute = false`, standard input closed, bounded output (`HookOutput.MaxBytes`, 16 KiB) and a process-tree kill on cancellation. `HookRunner` applies a 30 second default timeout.
- Consent is recorded per command hash: SHA-256 of the canonical JSON of the argument list, working directory, trigger and blocking flag (`HookCommandIdentity`). Editing any of them voids consent. `HookRunner` checks quarantine and then consent before it spawns anything.
- Quarantine is persisted by `PluginStateStore` (atomic temp-file write, then move). A plugin is quarantined when its activator throws or when the hook process runner itself fails. A non-zero hook exit is not a quarantine cause. `PluginHost.LiftQuarantineAsync` lifts it and records the operator's reason.
- `PluginHost` and `HookRunner` are constructed only in tests: a search of `src/`, `apps/` and `services/` for `new PluginHost(` and `new HookRunner(` found no product caller.

## Candidate chosen

Three tiers, each with its own boundary. No third-party sandbox or plugin framework is adopted.

1. **omp plugins (Ultrathink, Swarm, Substrate projector, GSD-Core adapter): out of the OmpOS process, inside the omp process.** OmpOS loads them with `omp acp --no-extensions` and one `-e` per extension (ADR-0012 D7), and only observes them. They have the privileges of the omp child. OmpOS adds no sandbox around omp.
2. **Harness plugins (manifest and hooks): out of process, declarative, consent-gated.** The host runs no plugin code in its own process. A hook runs as a child process only after consent for its exact command and only when its plugin is not quarantined. A blocking hook that cannot run (no consent, quarantined) denies the action rather than passing it.
3. **In-process UI contributions (commands, panels, tools, agent modes): in the host process, first-party only.** They are registered by compiled composition code, not loaded from disk. A third-party in-process plugin is not supported: there is no assembly loader, so there is no path for one.

Rule between tiers: anything from a source OmpOS does not compile must run out of process (tier 1 or 2) or not at all. This matches ADR-0004 decision 5, where MCP is the only out-of-process tool route for tools; hooks are the out-of-process route for lifecycle commands.

## Licence

The chosen design adds no package. The components that sit on either side of the boundary:

| Component | SPDX | Source | Verified |
| --- | --- | --- | --- |
| omp (`@oh-my-pi/pi-coding-agent` 18.12.5, the host of tier 1) | MIT | https://github.com/can1357/oh-my-pi/blob/main/LICENSE | `package.json` `license` field of the installed package, and the LICENSE file read from that URL, 2026-10-26 |
| Ultrathink plugin | AGPL-3.0-or-later | https://github.com/swcstudiospace/ultrathink | Recorded in `docs/plugins.md`; the repository was not re-read in this session |
| Swarm and Substrate plugins | NOASSERTION (no LICENSE file upstream) | https://github.com/swcstudiospace/swarm and https://github.com/swcstudiospace/substrate | Recorded in `docs/plugins.md`; not re-read in this session |
| OmpOS plugin host (`OmpOS.Harness.Plugins`) | NOASSERTION (OmpOS has no LICENSE file) | `docs/plugins.md`, "Licence facts" | Open: [OQ-62](../open-questions.md) |

The plugins stay separate programs launched by reference, so tier 1 does not link their code into OmpOS. Whether that satisfies the AGPL is a legal question this record does not answer ([OQ-62](../open-questions.md)).

## Decision

1. Tier 1, 2 and 3 are the only plugin tiers. A new kind of extension needs a new record or an update here.
2. A hook runs only when all of these hold, checked in this order before any spawn: its trigger matches, the hook is enabled, its plugin is not quarantined, consent exists for its current command hash. This is the existing `HookRunner` behaviour and it is the contract.
3. A blocking hook denies the action with a reason (`PluginQuarantined`, `ConsentMissing`, `ExitCodeTwo`) instead of letting it proceed. A non-blocking hook that has no consent or whose plugin is quarantined does not run, and does not deny.
4. A hook runs without a shell, with an absolute working directory, closed standard input, bounded output and a timeout, and its whole process tree is killed on cancellation or timeout.
5. Quarantine is persisted and survives a restart. Lifting it needs a non-empty reason, which is persisted with the lift.
6. OmpOS never copies, edits or links the source of a tier 1 plugin (ADR-0012 D1, D2).

## Verification route

Run by the orchestrator; none of the tests below was run when this record was written.

- Hook and consent behaviour: `dotnet test tests/OmpOS.Harness.Plugins.Tests/OmpOS.Harness.Plugins.Tests.csproj`. Relevant tests: `HookConsentTests.NoProcessIsSpawnedBeforeConsent`, `EditingTheCommandInvalidatesConsent`, `BlockingHookWithoutConsentDeniesTheActionAndSpawnsNothing`; `PluginQuarantineTests.BlockingHookOfAQuarantinedPluginDeniesWithPluginQuarantinedAndSpawnsNothing`, `LiftIsPersistedWithItsReasonAndSurvivesARestart`; `SystemHookProcessRunnerTests.ArgumentsReachTheProcessVerbatimWithoutAShell`, `StandardInputIsClosedSoAHookCannotWaitForTheConsole`, `OutputIsKeptUpToTheBoundAndFlaggedWhenCut`, `CancellationKillsTheWholeProcessTree`.
- Manifest rules: `PluginManifestParserTests` and `PluginHostLoadTests` in the same project.
- Tier 1: the recorded runs of ADR-0012 Phase 48 (one run per plugin and one end-to-end run with no model turn). They are a verification target of ADR-0012, not a recorded fact of this record.
- Tier 3: `tests/OmpOS.Contracts.Tests/Plugins/ContractsUiFreeTests.cs` and the Presentation plugin host tests.
- Index and structure of this record: `tests/OmpOS.ArchitectureTests/DecisionRecordTests.cs`.

## Open risks

1. **No OS sandbox.** `SystemHookProcessRunner` does not clear the environment, drop privileges or confine the filesystem: a consented hook runs with the user's privileges and inherits the host's environment, which can hold tokens. Consent is the only barrier, so the consent prompt must show the full command.
2. **Consent is per command, not per effect.** A consented hook may spawn anything its executable can. The hash covers the argument list, not the content of the executable it names. An executable replaced on disk keeps its consent.
3. **Working directory is not contained.** The manifest parser requires an absolute path and rejects NUL, and nothing checks that the path lies in the workspace.
4. **Quarantine covers runner and activation faults only.** A hook that exits non-zero every time is not quarantined. A plugin quarantined at load has no registered hooks, so lifting it takes effect only after a later load.
5. **Not wired into a running head.** The host and runner are built only in tests, so the boundary is enforced by the library, not yet by the applications. Until a head constructs them, no hook runs in the product.
6. **Tier 1 has no boundary of its own.** An omp plugin has everything the omp child has. It reaches OmpOS only through files and HTTP that it writes, which OmpOS treats as untrusted (ADR-0012 D9). A pin bump trusts the new commit; its review is a human step, and this record found no automated check of it.
7. **Licences of tier 1 plugins are unresolved** for two of three plugins (no LICENSE file) and unresolved in effect for the AGPL one ([OQ-62](../open-questions.md)).
8. **A symlink or executable swap between consent and spawn** is not guarded. The same window is recorded for the terminal gate in ADR-0006.

## Owner decisions needed

- Confirm the three-tier rule above, in particular that third-party in-process .NET plugins stay unsupported.
- Decide whether hook environments are cleared by default (risk 1) and whether the working directory must lie in the workspace (risk 3). Either is a code change in `SystemHookProcessRunner` and the manifest parser, outside this record.
- Decide where a head constructs `PluginHost` and `HookRunner` (risk 5).

## Alternatives considered and rejected

- **Load third-party plugins as .NET assemblies in the host.** Rejected: no isolation in the .NET runtime short of a separate process, and `AssemblyLoadContext` is not a security boundary. No loader exists today, which keeps the boundary simple.
- **Run every plugin in a container or an OS sandbox.** Not adopted: no sandbox technology has been verified for the three desktop heads, and a container runtime is not available on every head. It remains an option for risk 1; podman is the only container runtime the repository allows.
- **Treat omp plugins as harness plugins.** Rejected: omp plugins are code that omp itself loads (extensions), so OmpOS cannot gate them with manifest hooks.
- **Allow a blocking hook with no consent to pass.** Rejected: withholding consent would turn a guard into a pass (`HookRunner` remarks).

## Related

- [ADR-0004](ADR-0004-cross-platform-ui.md) decision 5, [ADR-0006](ADR-0006-terminal-control.md) (approval gate), [ADR-0008](ADR-0008-contract-versioning.md), [ADR-0012](ADR-0012-agent-loop-plugins.md), [ADR-0014](ADR-0014-mcp-client.md), `docs/plugins.md`.
