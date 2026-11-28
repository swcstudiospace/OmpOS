# Changelog

All notable changes to Autonogrammer are documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Versions before 0.15.0 are milestones of the internal roadmap (`.planning/ROADMAP.md`). They overlapped in time, and only 0.9.1 was closed formally: the date of each other version is the day of its last recorded work or audit. Phase numbers refer to that roadmap.

## [Unreleased]
## [0.20.21] - 2026-11-28

### Changed

- Phase 77 Wave 1 verification refresh (PR #42): 77-VERIFICATION.md records fresh current-HEAD evidence (CoworkAudit 16/16, StyleAccessibility 17/17, parity counts OK, docs gate 0 errors, API-half a11y HEAD bundle success with 0 errors) under `docs/runbooks/evidence/web-accessibility-2026-11-28/`. Gaps narrowed from 4 to 2: SEC-77-01/02 now fixed (SEC-77-04 still open, needs the Phase 74.1 review); all five Cowork runbooks exist and pass audit; the parity-audit elevation page with its SOTA section exists. Verdict stays `gaps_found` while SEC-77-04 and the human a11y items remain.

## [0.20.20] - 2026-11-28
### Fixed

- Desktop app opens standalone with no console window (PR #41): `apps/Autonogrammer.Native` built as a console-subsystem PE (`OutputType Exe`), so Windows allocated a console window on every launch. Flipped to `WinExe` (GUI subsystem); the startup trace listener now writes to `%LocalAppData%/Autonogrammer/logs/autonogrammer-<date>.log` instead of `Console.Error`, installed at most once per process and disposed (with its writer) when replaced.
## [0.20.19] - 2026-11-27

### Changed

- v0.17.0 Omp RPC engine rearchitecture (Phases 79-84, PR #40): RPC (`omp --mode rpc`) is now the primary local engine transport behind `IOmpSession` (default flipped Acp→Rpc at the Phase 84 cutover gate, ADR-0038 Accepted same commit; `Harness:Adapter=Acp` remains the supported rollback and remote boundary). Capability-based transport fallback: remote/approval-capable/MCP compositions stay on ACP per OQ-525; deny-by-default sessions compose over RPC by default (integration-audit fixes: remote composition + approval-capable default + approval-site ACP pins, all regression-pinned).
- Terminal engine retired (Phase 81): omp-TUI-in-PTY removed, `Harness.Herdr` deleted (provisioning `OmpManagedInstaller`/`OmpLocator` kept), `TerminalAutoRun` removed; persisted terminal records render retired/read-only in both heads (OQ-527 closed).
- Plugin code-connectivity (Phase 82): launch-plan byte parity, per-plugin command parity (Ultrathink first), realm initialised at `f28a3acc` (OQ-528 closed); MCP/wrapper/gate remain refusals (no observed RPC channel, OQ-526/OQ-526-83 open).
- ACP/RPC boundary (Phase 83): `Harness.Remote` ACP-only, `DirectSessionRunner` transport-agnostic, SSE serializer parity + Last-Event-ID resume; end-to-end PayloadJson mapper-shape divergence named as OQ-529 (open).

### Changed (tracked milestone; v0.16.0 stays tracked)

- Milestone audit `.planning/v0.17.0-MILESTONE-AUDIT.md` (tech_debt): 21/23 requirements satisfied; RPC-84-2/RPC-84-4 partial (bootstrap/web-smoke/secret-scan host-gated; tracker payloads posted to Linear but born as authored-only files). Owner decisions 2026-11-26: realm init, ADR-0039 Gate A=A1 + Gate B=B4. SEC-77-01 open at 0 waivers.

## [0.20.18] - 2026-11-23

### Changed

- Browser Use enablement wiring (phase 72, GSD v0.16.0 autonomous wave 3, stacked on PR #38): `BrowserUseSettingsValues` gains `Enabled` (default false), the Browser settings page edits it, values persist under `browserUse` in `settings.json` (bad enum names fail named), saves persist before mutating live options, and both `AgentHead` paths pass `Configure` from the persisted values so `browser_*` tools are offered only when the person turned Browser Use on and a Chromium binary is located. Live page/egress wiring stays gated (D-12/D-13, live CDP unproven); no live claim. Tests: Browser 23/23, Presentation Browser 15/15, Native Settings 451/452 (1 skipped).

## [0.20.17] - 2026-11-23

### Changed

- Phase 77 docs closeout (GSD v0.16.0 autonomous wave 2, stacked on PR #37): new runbooks `docs/runbooks/cowork.md`, `docs/runbooks/computer-use.md` and `docs/runbooks/v0.16.0-owner-actions.md` (every owner action in ADR-0030 form; Linear register SPE-34972), plus `docs/quality/v0.16.0-parity-audit-elevation.md` (SOTA record for the parity gate); all four listed in `docs/SUMMARY.md`. Docs-only, no product code under src/services/apps and no new tag.

## [0.20.16] - 2026-11-23

### Changed

- Refreshed `deploy/omp-deps.lock.json` (`@babel/parser` 7.29.14 to 7.29.15) via the sanctioned `OMP_DEPS_MODE=write` snapshot (PR #37, GSD v0.16.0 autonomous wave 1) so the push-only image-scan job can pass; verified locally with `snapshot-omp-deps: ok` in check mode and a full `deploy/api.Containerfile` build. No product code under src/services/apps and no new tag.

## [0.20.15] - 2026-11-22

### Changed
- Plugin boundary + Settings providers (Tc00c, PR #35): frontend confines Omp to backend (ADR-0001, PluginFrontendBoundaryTests); Settings typed select/connect/test flows (ADR-0002, ModelsFlowDisciplineTests); Linear SPE-36343..45 + Notion tracking.

## [0.20.14] - 2026-11-22

### Changed
- Verifier re-runs for phases 67/69/70/78 at 52eb2c55 (T8783, SDK 10.0.402) plus the phase 66 YAML frontmatter fix (now parseable): 70 passed 5/5; 67/69 stay human_needed; 78 stays gaps_found with SC6-only human checklist gaps (SC1-5 re-proven); all covered_digests recomputed to v1:sha256 (64-hex). Docs-only, no product code under src/services/apps and no new tag.
## [0.20.13] - 2026-11-22

### Changed
- Verification refresh for phases 66-77 (T855b-be, HEAD b46fd81f, SDK 10.0.402): 16 VERIFICATIONs refreshed with fresh targeted evidence (build 124 projects 0/0, format clean, Arch 107/107, Coordination 87/87 + Sessions.Postgres 148/148 vs throwaway services, Dispatch 127/127, Native 2353/2353, Presentation 2646/2646, a11y node 48/48); 70 upgraded to passed (5/5 ROADMAP criteria executed); 67/69 stay human_needed; the rest stay gaps_found with exact closing steps; all covered_digests recomputed to v1:sha256 (64-hex). 77 PLAN/SUMMARY count verified 11/11 (no new SUMMARY). Indexed upstream T855b req/arch/adr docs in docs/SUMMARY.md. Docs-only, no product code under src/services/apps and no new tag.
## [0.20.12] - 2026-11-21

### Changed
- Merge phases 66-78 docs lanes onto docs/verify-66-78: 44 SUMMARYs added, VERIFICATIONs completed for 66/66.2-66.4/68/71-77 (2 new, 11 re-verified); tree now 194 SUMMARYs, 47 VERIFICATIONs, 179 PLANs (every 66-78 phase has a VERIFICATION; only 72-10 lacks a SUMMARY as it is superseded by 72-12..14). Verification verdicts stay gaps_found where live/host criteria are unproven; no product code changed and no new tag.
## [0.20.11] - 2026-11-21

### Changed
- Phase 78 closeout: 78-VERIFICATION.md (rehearsal 44524125822 success; rc.1 + v0.20.0 assets verified); STATE.md updated; human checklist handed over as human-verification item.

## [0.20.10] - 2026-11-21

### Changed
- STATE.md closeout (AC-122): record T77d9 merge of PR #28/#29 at 0.20.9; SDK 10.0.402 installed; human L4 merge-ci.

## [0.20.9] - 2026-11-21

### Changed
- T77d9-be (PR #29) for v0.16.0 phases 66 to 78: GAP-OP-1 closed (80 operationIds, contract 1.1.0); JwtBearerSetup duplicate-definition merge fix from PR #28 sequencing.

## [0.20.8] - 2026-11-20

### Changed
- GAP-OP-1 closed: every operation in the API contract now carries an operationId (WithName in source, 80 added) and the contract is 1.1.0 (additive, minor). The build-time document is regenerated from source in CI; the committed docs/api/openapi.json is synced to the same 114 operationIds.
- Preserved dirty auth hardening: JwtBearer metadata refresh is explicitly 30 s (automatic refresh 10 min), bounding the post-rotation 401 window.
- T77d9-fe (PR #28) for v0.16.0 phases 66 to 78: preserved dirty auth/secrets files onto swarm/T77d9-fe from origin/main; gweb heads declare lang="en" and the artifact SVG fallback is labelled; upstream A02/A03/A04 artifacts indexed in docs/SUMMARY.md; session owner+surface index migration M0015.

## [0.20.7] - 2026-11-20

Phase 68 closeout: planning state reconciled. On the facts: 68-09/68-10 are superseded (their work items moved to planner drafts and owners); the account-erasure writer fence and migrations M0012/M0014 are built and SEC-77-07 is fixed on main; the stale HEAD-M0009 note now reads M0010-M0014 (head M0014).

  (No code change in this entry; the phase gate result is recorded in 68-VERIFICATION and the CI run on the closeout branch.)

  
## [0.20.6] - 2026-11-19

Phase 67 closeout: same M-fix set as reviewed on the branch (M-06..M-09, AG-67 rows reconciled, success criteria green).

- M-06 (`LinuxLibsecretTests`): a CANCELLED libsecret error is a locked store unless the caller cancelled.
- M-07: cancellation races the awaited native calls; callers return promptly (latency tests).
- M-08: `StateChanged` is raised under the state lock so concurrent mutations deliver in order.
- M-09: server-side client validation mirrors the heads' refusals (URL query/fragment; only one of URL/key).
- AG-67-1..24 traceability rows reconciled to Met update note; 67-VERIFICATION refreshed.

  
## [0.20.5] - 2026-11-19

Phase 67 closeout: M-findings fixed; the eight success criteria run green.

- M-06: a cancelled-while-not-collaborating libsecret error is a locked store, not a cancellation (`LinuxLibsecretTests`).
- M-07: cancellation races the awaited native calls in `BlockingSecretStore` and `WindowsDpapiStore`; callers return promptly while the native call finishes in the background (latency regression tests).
- M-08: `StateChanged` is raised inside the state lock so concurrent mutations deliver in order.
- M-09: the server's client validation now rejects the same shapes the heads reject (project-URL query/fragment; client block with only one value).
- AG-67-1..24 traceability rows reconciled to Met; 67-VERIFICATION refreshed.

  
## [0.20.4] - 2026-11-20

### Changed
- Phase 66.3 closeout (web accessibility WCAG 2.2 AA): all 48 a11y node tests and the fixture self-test now run in CI (roadmap and AG-AX-5 say 48 tests); the conformance statement, the conformance table and the known issues are refreshed on the current tree (0 error findings on both halves; Demo 12 warnings, API 48 warnings of which 36 are the optional states of signed-in surfaces, K18); new surfaces (Dispatch checked in the API smoke; account menu, Cowork approval, dock tabs and the artifact panel reported as `state-unreachable` until a sign-in path exists, OQ-312); guide texts no longer say "connect screen"; OQ-191 (first-load flake) decided accepted-with-retry and the passing smoke JSON kept under `docs/runbooks/evidence/web-smoke-2026-11-20/`.

## [0.20.3] - 2026-11-20

### Changed
- Phase 66.2 closeout (hosted real web app): the docs no longer claim an owner-token web sign-in — the web head signs in with Supabase passwordless since Phase 67 (the connect screen and `OwnerTokenSession` were retired by 4a234284; the API `OwnerToken` mode remains). `run-local.sh` (real `omp`) evidence re-run on HEAD (`docs/runbooks/evidence/hosted-web-2026-11-19/`, `result: passed`)
- CI now runs `node scripts/hosted/selftest.mjs` and the `check-no-demo.sh` trio (real bundle passes; Demo bundle with `--expect-demo` passes; Demo bundle without the flag fails).

## [0.20.2] - 2026-11-20

### Changed
- Phase 66.1 closeout: the scaffold-phase verification record (AG-SC-6) is closed by CI proof on the merged tree (runs 44532185561, 44541692228 on main; PR #19 head runs 44566723144, 44566709822): the CI android job restores the hand-patched Android lock in locked mode and builds green, and the `./bootstrap.sh` gate is mirrored by CI. Phase 66.1 is marked complete in ROADMAP.

## [0.20.1] - 2026-11-20

### Changed
- Rebrand verification gate (Phase 66 closeout): `rebrand.py verify` (exit 0 means nothing rewritable remains) and the 61 rebrand unit tests now run from `scripts/ci/repo-checks.sh`, and the historical strict-equality comparison against the 2026-11-04 baseline census is retired — it cannot hold after v0.16.0-v0.20.0 shipped. The ROADMAP success criterion and the rebrand runbook now name the durable exit-0 check, and the three remaining bare old-name tokens (release plan, OQ-63, OQ-102) are reworded.

### Fixed
- CI: the `dotnet list --vulnerable` scratch output `vuln.json` is gitignored. Its absolute runner paths would otherwise appear as rewritable old-name tokens to the rebrand verify gate wired into repo-checks.

## [0.20.0] - 2026-11-19

### Added
- Cowork artifact cards: a successful `artifact_create` or `artifact_update` opens its version in the artifact panel. Before this, the card reached the transcript, where no native view was mapped for it. Delivered versions run without network access, because artifact origins are not persisted yet; the web head does not show artifacts yet.
- Windows installer: `Autonogrammer-<version>-win-x64-Setup.exe` installs like traditional software. Double-clicking it asks for administrator rights (UAC) and installs the 64-bit app to `C:\Program Files\Autonogrammer`, with a Start Menu entry for all users, an optional desktop shortcut, a licence page, a launch-after-install option, an Apps and Features entry and an uninstaller. The uninstaller asks whether to remove settings and sessions; the default is No. Installing a newer version over an older one keeps the folder and the user data, and an all-users install removes an older per-user install of Autonogrammer. `/CURRENTUSER` still installs per user without administrator rights, and `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` installs silently. The installer is not signed with a publisher certificate, so Windows SmartScreen may warn. No job had run it on Windows when this was written (see `docs/runbooks/desktop-release.md`).
- `scripts/release/windows/smoke-test-installer.ps1` installs the setup silently on a Windows runner, checks the files, the registry entry and the shortcuts, launches the app for 15 seconds, checks that it wrote nothing beside its executable, upgrades in place, uninstalls, and checks the per-user migration.
- `scripts/release/verify_desktop_assets.py` checks the six desktop files (names, versions, sizes, file signatures and, for the tar.gz, zip, deb and exe, that the archive is structurally complete) and writes `SHA256SUMS-desktop`. Its 58 unit tests run in `repo-checks`.
- `desktop-release` takes an optional `version_suffix` input to rehearse a prerelease from a branch. The run uses the prerelease file names and applies the same gates as a tag build (the private plugin credential and the plugin licence check), and it creates no release.
- Install guide `docs/guides/install.md` with a manual test checklist for Windows and macOS.

### Changed
- Diff review: the line diff, the failed-call rule and the net-edit rule each have one implementation, shared by the Changes bar, the checkpoint timeline and the file-edit card.
- Hook run status names are single-sourced in `HookRunStatusNames` and shared by the projector and the log.
- Remote environment profiles: the three editors share one recompute path and one identifier rule.
- Cowork: the 200-session cap is defined once in Contracts (`AgentSessionLimits.MaxSessions`), and the Cowork message derives from it.
- Customize empty states list the folders discovery scans, read from `ConventionDirectories`, which moved to Contracts.
- Cowork task rows are documented in group order, and the dispatch host failure message is documented as diagnostic, not shown.
- Releases: `release.yml` creates the tag release as a draft, and `desktop-release.yml` publishes it only after the Windows, macOS and Linux files are attached and verified and the image archives, SBOM and checksums exist. A failed desktop build now leaves a hidden draft, not a public release without installers.
- The private `agent-substrate` plugin is checked out in release builds with a read-only deploy key (`SUBSTRATE_DEPLOY_KEY`) or with `SUBMODULE_TOKEN`. A tag build fails when neither exists (OQ-63).
- `agent-swarm` and `agent-substrate` carry the `AGPL-3.0-or-later` LICENSE (OQ-102). Each pin moved by that one file, and the plugin manifest now expects it. The source of the private `agent-substrate` repository ships inside public installers under the AGPL.
- The macOS app bundle is ad-hoc signed before it is zipped and put into the `.dmg`. It is not notarized.
- The app starts in your home folder when it is launched from its install folder, a system folder, a filesystem root or a missing folder, so a launch from Program Files does not make the install folder the workspace. The WebView2 data folder is `%LOCALAPPDATA%\Autonogrammer\webview2`, and MCP connection checks no longer use the install folder as their working directory.

### Fixed
- Agents rail: closing an agent that is not selected asks first when it is running, as the selected row does. A running agent that fails to open stays open with a status message.
- Diff review: Apply cannot write another session's hunks after a session switch or when that session's pane is removed.
- Install: a deferred, rolled-back, offline or up-to-date install shows its own message instead of "Install failed".
- Queue: a queued message whose start fails reports the failure instead of stopping the queue silently.
- New session picker: an environment whose last test failed shows the failure and cannot be chosen to start. The picker reads the same result as the Remote environments page. The cloud option gives the real reason (not set up, or configured).
- Closed sessions land in the Archived column for every runtime status. A closed running session without an engine reads Closed.
- Dispatch: a frame with a known type that fails validation no longer answers `unsupported_frame`.
- Remote environments: an SSH test outcome carries over only when every field it depends on is unchanged. A container test outcome is invalidated when the podman path changes.
- Cloud: the dialog and `/in-cloud` report the same outcome for an unconfigured host, and the failure-kind mapping lives in one place.
- Privacy and About: the Privacy page no longer says Autonogrammer has no accounts. Privacy statements come from one source. The web About view offers the source with the desktop wording and always shows the source link.
- Bundles and connectors: a project-scope notice shows user text, not an internal token. Connector labels round-trip to the stored policy.
- Side chats: closing a side chat removes it from the panel list.
- Automations: a failed change publishes its state the same way a failed load does. Validation text names the enforced limits.
- Commands: the `/open` and best-of-n descriptions match what the commands do.
- Safety: the entry-limit message names the enforced limit.
- Catalog: the Lost group's documentation matches the projector rule, including stale approval-wait sessions.
- Hook export names are exhaustive. An unlisted scope or outcome no longer exports as `plugin` or `invalidResponse`.
- Tag runs of `desktop-release` for v0.17.0 and v0.19.1 failed at the private plugin checkout because no credential existed, so no release received the Windows installer or the `.dmg`.

### Removed
- The unwired hooks view chain (`HooksViewModel`, `HookRowModel`, `HooksViewState`, `HookBlockReason`) and the hooks source contract with its Web implementation (`IHooksSource`, `HookEntry`, `HookQuarantineEntry`, `HooksSourceException`, `WorkspaceHooksSource`).
- The unused `/logs` handler (`LogsSlashHandler`) with its folder-opener interface and copy.
- The Native diff entry types `DiffEntry` and `DiffEntryKind`.
- Browser pane states that nothing published.
- `AgentSessionRegistry.MaxSessions`, replaced by `AgentSessionLimits.MaxSessions`.

## [0.19.2] - 2026-11-18

### Fixed
- The offline retry test pins its jitter, so its retry schedule is deterministic and no longer depends on random jitter.

## [0.19.1] - 2026-11-18

### Added
- Provider catalogue: 31 omp-native rows, each `unverified` with a note that begins `Verify before use:`; verification is tracked as KL-13. Seven researched rows are not added (KL-16).
- Routeless omp-native rows accept an API key and send it to omp by provider id, in the API, the native settings and the presentation card (ADR-0019, amended 2026-11-18).
- ADR-0013 (plugin isolation) accepted by the owner, 2026-11-18.

### Changed
- A hook gate that does not answer within 5 seconds denies the action.
- A prompt submitted while a model or option switch is in flight waits for the switch instead of failing.
- A cloud create or hand-off cancelled after its request was sent reports an unknown outcome and is not retried.

### Fixed
- Shell scanner: deny rules see commands behind a quoted substitution, a backslash-newline continuation, an ANSI-C quoted name, `env -S`, and wrapper options that take a value (`env -u`, `env -C`, `timeout -s`); known-safe commands with unquoted glob words ask.
- ACP: an over-long stderr line is cut at the bound; a failed WebSocket connect no longer blocks the next attempt; an all-omitted image set is refused rather than sent as a note alone.
- Hooks: a failClosed omp hook on a guarding event such as `tool_call` blocks when it fails; a damaged `hooks-host.json` fails the open instead of substituting defaults.
- Sessions: an unreadable state file is kept and no later write replaces it; unknown lifecycle words survive a rewrite; titles are cut on whole characters.
- Plugin consent: approval records the declaration the prompt showed, and a changed declaration is refused.
- Browser: a take-over releases keyboard capture and the gate on close.
- Settings and views: display saves write only the switches that changed; bundle and skill toggles snap back after a refused write; network diagnostics and browser acquisition show their failures; connectors settings loads servers; Projects rename sends the typed name; Kanban does not re-read details on reattach; artifact images keep the previous size cap.

### Removed
- The dead Native hooks view chain (`HooksModel`, `HooksView`, `HookRow`, `HookRegistrySource`); the live surface is `HooksPageView`.

## [0.19.0] - 2026-11-17

### Added
- Herdr session client: `HerdrCliSessionClient` runs create, status, list and terminate against the installed herdr CLI; the live test passes on a host with herdr (`AUTONOGRAMMER_LIVE_HERDR=1`).
- Provider catalogue: `scaleway` (`SCW_SECRET_KEY`), `meta` (`MODEL_API_KEY`) and three regional xiaomi rows (cn, sgp, ams), aligned to omp 18.22.0.

### Changed
- Herdr pause, resume, attach and detach report unsupported, because herdr 0.9.5 has no such commands; they no longer fake success.
- Trust All skips quarantined, disabled and not-applicable hooks (behaviour change).
- A cowork launch with contributed overlay entries writes `omp-overlay.yml` (behaviour change).
- Key-less omp-native rows (mistral-vibe-plan, kimi-code) refuse a credential write.

### Fixed
- Shell auto-review: known-safe reads refuse unexpanded `$` and `~user`; a chained allow rule approves only when every segment matches; `find -exec` and `-ok` are expanded; the ACP kind is read before the MCP name.
- ACP: a turn cancel releases a parked permission; a read-then-write keeps CRLF line endings.
- Native: a late Allow after a denial stays denied; connector saves keep deny rules; git mention diffs refuse secret-looking files; plugin-root skills are readable.
- MCP consent shows the full command, and a reload clears a pending consent.
- Sandbox: a failed agentOS start tears down its actor; podman follow-mode logs finish when the container exits.
- Memory: a pause is re-checked under the account lock before unkeyed writes.
- Launch: contributed overlay entries are written to the launch file.

## [0.18.0] - 2026-11-17

### Added
- Realm is the fifth omp plugin: `plugins/agent-realm` is a submodule pinned to `f28a3acc` and launched as an omp extension after GSD-Core, with its default `fail-closed` policy in force. It needs `npm ci && npm run build` in its directory before it loads, and it has not run in a live omp session.
- Recorded plugin contracts (`tests/Autonogrammer.Harness.AgentLoop.Tests/Fixtures/OmpPluginContracts`) and the `OmpPluginContractTests` gate: commands, tools, environment variables, schema identifiers, package identity and pins are checked offline; evidence literals and manifests at the pin are checked by Live tests.

### Changed
- The plugin catalog, launch planner, head copy and submodule invariant cover five plugins. `AgentLoopSettings.RealmEnabled` switches Realm, and omp launches pass `-e plugins/agent-realm` after the GSD-Core plugin.

## [0.17.0] - 2026-11-16

### Added
- Phase 75 Cowork parity A has a launch-contributor seam and a parity inventory. Production standing-context, memory-tool registration and scheduled-session integration remain incomplete.
- Enabled Cowork project, instruction and memory APIs use PostgreSQL; project-scoped memory writes verify ownership and serialize concurrent limit checks. Durable cross-instance idempotency remains open.

### Fixed
- The Linux Bubblewrap probe now runs its executable from a read-only root instead of misreporting an empty filesystem as disabled user namespaces. The session wrapper still fails its protected live launch test and is not verified.
- Artifact sync-off metadata no longer includes a grants field; the current API accepts the reduced payload without changing existing grants. Compatibility with older deployed receivers has not been verified.
- Account endpoints default off; existing installations requiring account and Code board routes must set `Accounts__Enabled=true` before upgrading. Anonymous requests remain unauthorized. The transition has not been deployed or live-verified.
- Restored host-observed facts and a single deny timer in the Computer Use approval model.
- Cowork dock tabs support roving keyboard navigation and correct F6 region cycling.
- Cowork no longer reports a stale gateway-loading notice after startup and preserves the actionable gateway failure notice.
- Cowork launches no longer inherit Code-session state paths and reject non-provider environment variables from contributors.
- Artifact access dialogs capture focus, deny on Escape and request focus restoration when closed; the production opener remains unverified.
- Multiple Dispatch approval panels now use distinct accessible heading references; integrated browser focus remains unverified.
- Restored fail-closed browser stage-two approval; per-session policy, audit and egress integration remain open.
- Chromium pipe connections start their receive loop, and cancellation terminates a stalled launched process. An isolated headless smoke passed with a test-only no-sandbox opt-in; guarded production browsing remains unverified.
- Dispatch pairing rejects file-backed and other non-OS secret stores for device keys and credentials; native pairing and platform keyrings remain unverified.
- PostgreSQL Dispatch store transaction handling covers duplicate devices, replay and retention; production Dispatch remains disabled pending migration registration and idempotency-key isolation.
- Sign-in coordinator defers synchronization-resource disposal until registered sends finish.
- Artifact version uploads apply the configured per-kind byte limit (a later version uses the stored kind) and `MaxVersions`, and require the next version number; a version that skips ahead is a `409` conflict instead of a history that fails its integrity check.
- PostgreSQL artifact writes take a per-account database advisory lock before reading usage, so parallel uploads cannot exceed the account quota and concurrent writes of one version resolve to same-hash or conflict. A write reads usage with aggregate queries and loads only the artifact it changes.
- A desktop refresh no longer overwrites or removes a newer sign-in another process stored, and no longer brings back a session another process signed out; sign-in and sign-out now take the cross-process refresh guard (sign-out waits at most 5 seconds for it). Two-process behaviour has only been exercised with two managers over one store.
- Account enrollment no longer holds the request for the account write: only account routes wait (at most 2 seconds) for their caller's write, other routes continue at once, concurrent first requests share one pending write, pending writes are bounded, time out after 10 seconds and stop when the host begins stopping.

### Changed

- Merged `main` (0.16.1 to 0.16.3: desktop installers, split checksum filenames, managed Omp install, JavaScript interop stack logging, Dispatch relay) into the Phase 66 to 77 line; migration M0011 stays `dispatch_sweep_indexes` from `main`, M0012 is the account erasure fence, and the Cowork memory idempotency migration is now M0013 (its checksum is unchanged; its fence trigger is M0014). A database that applied the unreleased integration build with memory idempotency as version 12 must be rebuilt before it can migrate.

### Security

- The API and worker images move to runtime `10.0.15` and desktop packages are built on it, to address CVE-2026-114146, CVE-2026-114147 and CVE-2026-114149 (HIGH) reported by the image scan for runtime 10.0.14.
- Account erasure is fenced in the database (SEC-77-07, migration M0012). The eraser takes the account lock exclusively, which waits for every writer that was already admitted, and commits a permanent `account_erasure_fence` row (a digest of the account, never the identifier) before it deletes anything. A trigger on every account-owned table (accounts, sessions and their events and approvals, cloud agents, Dispatch devices, commands and feed, Cowork events, projects, instructions, memory entries, settings and claims, artifacts, versions and grants) then refuses every insert and update for that account with SQLSTATE `AE001`, whichever store, job, older binary or direct SQL sends it; the stores that share the PostgreSQL pipeline raise `AccountErasingException`. The families are deleted behind the fence, the account row and the fence row move to `erased` in the same last transaction, and a repeated call resumes a failed erasure and re-runs the tenant-key deletion. Rollback of M0012 and M0014 is refused while the fence holds any row. Not covered: the Dragonfly coordination keys (at most 24 hours, no user content), a production entry point for erasure and identity-provider session revocation; see SEC-77-09 in `docs/quality/v0.16.0-security-review.md`.

## [0.16.3] - 2026-11-08

### Changed

- A JavaScript interop failure in the web head logs the frames of its stack, never its message, and the next smoke failure names the call that threw. Other failures still log only the exception type.

## [0.16.2] - 2026-11-08

### Added

- Desktop installers. Windows: an Inno Setup installer that installs per user, adds a Start Menu entry and an optional desktop shortcut, and whose uninstaller asks before removing settings, sessions and the managed Omp runtime (the default is to keep them). Linux: a `.deb` that adds a menu entry and an `autonogrammer` command. macOS: a `.dmg` with a link to Applications. All three are unsigned and not notarized. Each is verified only when the workflow runs on its own OS.
- Plugin bundling. Release packages carry the pinned plugin trees under `plugins/` beside the executable. Release builds fail closed until every required plugin has a licence file and the private substrate plugin's token is provisioned.
- Omp install and update. The Code landing's Local machine card offers Install Omp, with progress, cancel and retry, and checks for a newer `@oh-my-pi/pi-coding-agent` after the window shows, on each launch. Installs verify the tarball's sha512 and the pinned Bun's SHA-256, health-check the new copy, and roll back to the last good version when a check fails. An offline start shows the installed version with a stale notice. Local sessions, CoWork sessions and the terminal resolve Omp at each launch; a remote session (ssh, container or WSL) runs the omp on its own host and does not need a local one. Side chats use the path from the last launch.
- CoWork sign-in entry. A signed-out Code/CoWork switch now appears. CoWork shows the sign-in message and a sign-in button, or the not-configured message when no sign-in service is configured.
- Code landing. With no session selected, the Code surface shows four environment cards: Local machine (with the Omp status it found), another machine linked to this account, Cloud, and Remote (the saved remote environments). Linked machines have no device source yet, and the remote Omp status reads "not checked yet" until the remote check is wired to the UI.
- Remote provisioning service. It checks an SSH environment for Omp, Bun and the bundled plugins, reports drift, and installs only what has drifted, after a per-environment confirmation. It is not yet reachable from the UI.

### Changed

- Pull requests into main must raise `VersionPrefix` above the base and add its CHANGELOG section. `scripts/ci/check-version-bump.sh` runs in the CI job and fails the pull request otherwise.
- CoWork sessions are stored per account at the host. Each account's session files live in its own folder, and a host refuses to create or open a session that another account's registry does not own. The head keeps the signed-out gate, so no CoWork task is created in this release, and the session list does not yet follow sign-in or sign-out.
- The CI browser smoke continues on error, so the native tests and the other steps still run and report when it fails. A final gate step fails the job when the smoke failed, so the smoke still gates the job.

### Fixed

- Desktop checksums are published as `SHA256SUMS-desktop`. `release.yml` publishes image and SBOM checksums as `SHA256SUMS-images`, so a tag push cannot replace one file with the other.
- The API and worker images move to runtime 10.0.14 and upgrade apt, to address GHSA-p3jp-9v83-76w9 and CVE-2026-109518 reported by the image scan.
- The Android project's lock file restores in locked mode again.
- Packaged apps find `plugins/` beside the executable.
- The web head sends prompts and the event stream only to `Api:BaseUrl`, or to a `?apiBaseUrl=` origin listed in the build-time `Api:OverrideOrigins` (empty by default). Any other override, including a loopback origin that is not listed, is refused.
- The ACP transport stops `omp acp` when a stdout line passes its cap (1 MiB by default, a constructor parameter) and fails the session with a malformed-frame error, instead of leaving the child running.

## [0.16.1] - 2026-11-07

### Security

- Desktop packages are built with runtime `10.0.14` instead of `10.0.13`, so they do not bundle GHSA-p3jp-9v83-76w9 (CVE-2026-111497, HIGH, out-of-bounds write in the .NET Brotli decoder). `v0.16.0` has no desktop installers attached for that reason. The packages stay unsigned and not notarized.

## [0.16.0] - 2026-11-07

### Added
- Desktop packages for Linux x64, Windows x64 and macOS arm64. A `v*` tag builds them in `desktop-release` and attaches `Autonogrammer-<version>-linux-x64.tar.gz`, `Autonogrammer-<version>-win-x64.zip` and `Autonogrammer-<version>-osx-arm64.zip` to the GitHub Release. The packages are self-contained, unsigned and not notarized. omp and Bun are not inside the archive.
- Cowork artifacts on the web head (Phase 71, plan 06): an artifact card opens a panel and never renders content inline. Interactive HTML loads from a 60-second ticketed render document in a sandbox with exactly `allow-scripts`, `referrerpolicy="no-referrer"` and an empty `allow`; the frame is destroyed on a second load (the navigation is reported as blocked) and when the sign-in ends. Static HTML, sanitized SVG and Mermaid render through `srcdoc` under the page policy and are labelled static view. Mermaid 12.2.0 is vendored unmodified (SHA-256 `a8a6a6f8…c3b9`) with EPL-2.0, Apache-2.0 and ISC libraries enumerated in its notice (OQ-400 admitted by default). The CSP gains `frame-src` for the API origin only through `scripts/vercel/render-csp.sh --frame-src`, and the offline shell never precaches `lib/mermaid/`. The smoke run gains an `artifact` half that frames the hostile fixtures and fails on any outbound request, frame navigation or parent access. Not run on a browser or a device in this lane (see the phase summary).
- Surface switch and Cowork task list (Phase 69): the native title bar and the web top bar switch between Code and Cowork by pointer or keyboard (Ctrl+Shift+1 and Ctrl+Shift+2, Meta on macOS), and the web head opens Cowork directly at its `/cowork` address. The choice is remembered on the device. A switch hides a surface without stopping its sessions, and a hidden surface shows a chip when it has running, waiting or failed sessions. Cowork has its own rail for tasks and projects, a conversation and a collapsible dock. Its task list holds 200 tasks per account; a full list disables "New task" and says why. Signed out, Cowork shows "Sign in to use Cowork", and a head without Cowork services shows no switch.
- Cowork plugins, isolation, usage and notifications composition (Phase 76): bundle skills feed convention plugin roots, bundle MCP servers enter the catalog switched off until confirmed, Cowork sessions can carry an isolation wrapper and Code sessions do not, and the desktop settings and web information pages name the limits. Parity rows that depend on omp consuming a flag stay partial. `docs/parity/cowork-parity.md` was not in the tree, so those rows were not graded here.
- Cowork browser use on the desktop head (Phase 72): the head binds `Cowork:BrowserUse`, registers the browser tool provider only when the browser is enabled and located, and offers the `browser_use` capability only when dispatch sessions are also allowed. Stop is Ctrl+Shift+. The hosted API and Web head do not launch a browser. A live omp turn with the gateway, an image result and `browser.enabled: false` was not run (OD-07 not approved).
- Cowork chat core (Phase 70): `CoworkSessionHost` runs one local omp child with `EngineEnvironmentMode.Allowlist` and provider keys as its only explicit variables. The Cowork resolver is installed through `EngineCompositionOptions.PermissionResolverFactory` with all ten ACP kinds. `CoworkHead` is composed in `AgentHead`, not `Program.cs`. A gateway health-check failure starts the session without Cowork tools and shows `Cowork tools are unavailable:` plus the reason. Under Allowlist, tools and a catalog stdio MCP server no longer inherit variables such as `SSH_AUTH_SOCK`, `GIT_*`, `XDG_*` or `DISPLAY`; a server that needs a variable declares it in its own entry.
- Passwordless sign-in on the hosted web head (Phase 67): a code or a link signs the tab in against the auth host, with the token and verifier held in memory only (a reload signs out). The returning tab hands the code over a same-origin channel and scrubs the query before the page renders. A mismatched auth origin is refused. The content security policy can name a second connect-src origin when sign-in is enabled; the Demo build does not.

- Hosted web app (Phases 66.2 to 66.4; ADR-0021): the web head can be built as the real application with no Demo code (`scripts/vercel/build-web.sh`, asserted by `scripts/vercel/check-no-demo.sh`, which has an `--expect-demo` positive control for the Demo bundle) and used against a hosted API. Nothing is deployed by this change: the public URL still serves the Demo until the owner approves the switch (`docs/runbooks/deploy-and-rollback.md`).
- Sign-in for one operator: `Api:Auth:Mode=OwnerToken` accepts a bearer token whose SHA-256 digest is listed in `Api:Auth:OwnerToken:Sha256` (constant-time comparison, a failure throttle that answers 429 with `Retry-After`, rotation by listing two digests); `Api:Auth:Mode=Oidc` stays the default. `GET /auth/config` (anonymous, `no-store`) gives a client the mode, authority and audience. The web head has a connect screen, an auth gate and a sign-out button; the token is held in memory only and a 401 ends the session.
- Access control for hosted use: `Api:Auth:AllowedSubjects` and `Api:Auth:AllowAnyAuthenticatedUser`, a 403 `subject_not_allowed` problem, and a per-subject partition on all six rate-limit policies.
- API hosting hardening (`Hosting:*`): forwarded headers (`None`, `TrustAllSingleHop`, `KnownNetworks`), HSTS, security headers with a strict content-security policy for API responses, Kestrel limits, request timeouts (a 504 problem; event streams are exempt), a readiness flip on shutdown, `GET /health/startup`, SSE keep-alive comments and `retry: 2000` on shutdown, JSON logs and an OpenTelemetry resource.
- Engine isolation on the direct-run path: `Harness:Acp:Environment` (an allow-list outside Development), a 0700 workspace per session under `Harness:Acp:WorkspaceRoot`, a non-dumpable API process (`Hosting:RestrictProcessInspection`), and `Harness:Acp:Approval` (`Kinds`, `GrantableTools`, `DecisionTimeoutSeconds`; deny everything by default) so the tool-approval endpoints can grant a tool there.
- Web shell: routes (`/`, `/kanban`, `/sessions/{id}`, `/settings[/{page}]`, `/about`, `/connect`), a title per view, a route announcer and route focus, an error boundary, a not-found page and an About page; a transcript that renders the latest 50 turns with a "Show earlier messages" button and batched announcements; an offline app shell (a service worker that never caches cross-origin requests, `Authorization`, event streams or `/auth/*`); loading and `<noscript>` text.
- Accessibility (target: WCAG 2.2 AA, Phase 66.3): forced-colors and `prefers-contrast` styles, scroll padding, 24 px targets, reflow at 560 and 360 px, text spacing, persistent live regions, heading fixes, and scripted checks (`scripts/web-smoke/a11y/`: routes, keyboard walk, media emulation, reflow, text spacing, target size, states, connect) that the web smoke runs. `docs/accessibility/` holds the conformance statement (partially conformant), a 55-row WCAG table, a keyboard script, a screen-reader plan that has not been run, and the known issues.
- Hosted end-to-end harness (`scripts/hosted/`): a scripted OpenAI-compatible model server, an isolated omp home, `e2e-api.mjs`, `e2e-browser.mjs` and `run-local.sh`, which drive the real `omp acp`.
- Supply chain and CI: a generated SPDX SBOM (`docs/legal/sbom.spdx.json`, `check-sbom.sh`), base images pinned by tag and digest with a `HEALTHCHECK`, `scripts/ci/scan-images.sh`, `check-skip-markers.sh` (a skip marker needs a SUP row), an OpenAPI breaking-change check, a snapshot script for the omp dependencies, job timeouts and least permissions in `ci.yml`, an artifacts job in `release.yml`, and CodeQL and OpenSSF Scorecard workflows (SHA-pinned; never run).
- Operations and security documents: the hosted threat model, the data inventory and retention, SLOs and alerting, incident response, backup and restore, deploy and rollback, Railway Config as Code files in `deploy/railway/`, ADR-0021, and the record of the integration of the rename line (`docs/runbooks/integration-autonogrammer-2026-10-29.md`).
- Brand: the wordmark in two layouts with "Auto" in the text colour and "nogrammer" in the accent colour, the six SVG sources and the assets regenerated for the name Autonogrammer.
- Scaffold of milestone v0.16.0 (Phase 66.1): 21 behaviour-free source projects (`Autonogrammer.Resilience`, `Autonogrammer.Cowork.Abstractions`, `Autonogrammer.Harness.ComputerUse.Abstractions`, `Autonogrammer.Harness.Secrets.Os`, `Autonogrammer.Accounts`, `Autonogrammer.Cowork`, `Autonogrammer.Cowork.Artifacts`, `Autonogrammer.Cowork.Artifacts.Stores`, `Autonogrammer.Harness.BrowserUse`, `Autonogrammer.Harness.ComputerUse` with its `X11`, `Windows` and `MacOS` backends, `Autonogrammer.Cowork.Memory` with its `Postgres` store, `Autonogrammer.Harness.PluginBundles`, `Autonogrammer.Harness.Isolation`, `Autonogrammer.Harness.Notifications`, `Autonogrammer.Accounts.Desktop`, `Autonogrammer.Dispatch.Client`, `Autonogrammer.Dispatch.Desktop`) and their 19 test projects, each with `AssemblyMarker` only; registered in `Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `LayerMap` (62 rows, six new same-layer tuples), `ProcessSpawningAssemblies` (seven names) and `docs/architecture.md`.
- Contract version 1.6.0: `ContractVersion.Current` moves from 1.5.0 to 1.6.0 with no plugin contract change, guarded by the Plugins-versus-Ui isolation test.
- Launch seams in `Autonogrammer.Harness.Abstractions` (`ILaunchContributor`, `LaunchRequest`, `LaunchContribution`, `IProcessLaunchWrapper`) for the cowork and isolation phases; extended by Phases 75 and 76, never recreated.

### Changed

<!-- rebrand:keep-start -->
- The product is renamed from OmpOS to Autonogrammer (milestone v0.16.0, Phase 66, mechanical pass). Renamed: the product name and display strings, the C# namespaces and assembly names, the projects and files (`Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `src/Autonogrammer.*`, `services/Autonogrammer.*`, `apps/Autonogrammer.*`, `tests/Autonogrammer.*`), the environment variables (`OMPOS_*` to `AUTONOGRAMMER_*`), the HTTP header `X-Autonogrammer-Contract-Version`, the convention directory `.autonogrammer`, the PostgreSQL schema `autonogrammer`, the Dragonfly key prefix `autonogrammer:`, the plugin directory `plugins/autonogrammer-hooks-omp` and the Android `ApplicationId` (`com.swcstudiospace.autonogrammer`). The external omp engine names (the `omp` binary, `omp acp`, `~/.omp`, `OMP_*`, `models.yml`) and the first-party engine identifiers such as `OmpLauncher` are kept. The names of the existing GitHub, Vercel and Railway services, and the URLs that contain them, are unchanged until the owner renames those services.
<!-- rebrand:keep-end -->
- Migration: user data is to be migrated once from the old names (data directory, convention directories, state); that runtime migration is planned as a later unit of Phase 66 and is not part of this change, so the renamed tree does not read the old locations yet. The Android `ApplicationId` is a new identity, so an installed app does not upgrade to it.
<!-- rebrand:keep-start -->
- Owner actions after the rename: rename the GitHub repository, the Vercel project and the Railway services and then update the URLs; rename the `OMPOS_*` variables to `AUTONOGRAMMER_*` in Vercel, Railway, GitHub secrets and local `.env` files; recreate or migrate the podman containers, volumes, secrets and image, the PostgreSQL schema and its advisory-lock key and the Dragonfly keys, and deploy the API and the worker together; change the three plugin submodules that may read the old names in their own repositories; confirm the legal wording and the trademark.
<!-- rebrand:keep-end -->
- Breaking: an OIDC deployment outside Development must set `Api:Auth:AllowedSubjects` or `Api:Auth:AllowAnyAuthenticatedUser=true`; startup fails otherwise.
- Outside Development the engine process starts with an allow-listed environment instead of the API's whole environment; `Harness:Acp:Environment:Mode=Inherit` there needs `Harness:Acp:AllowInheritedEnvironment=true`.
- `scripts/web-smoke/size-budget.json` is now measured on a real publish of the web head (2026-10-29: 12.4 MB raw, 4.8 MB gzip, 3.8 MB Brotli) with 10 percent headroom, instead of a provisional ceiling taken from the Avalonia spike; the budget check no longer reads a comment in a stylesheet as an import.
- Breaking: outside Development the API stops at start when the non-dumpable setting (`Hosting:RestrictProcessInspection`, on by default) cannot be applied, instead of continuing; `Api:Cors:Origins` entries must be `scheme://host[:port]` and are checked at start.
- CI runs on GitHub-hosted runners (`ubuntu-24.04`, `windows-2025`, `macos-26`) instead of the Omega self-hosted pool. The Linux job installs rootful podman and Chrome; desktop jobs publish the Avalonia head on Linux, Windows and macOS. Docker is still not used. `macos-14` retired on GitHub Actions on 2026-11-02, and the desktop-release workflow moved to the same three labels.
- GitHub-hosted restore uses a private SDK 10.0.402. The runner image's runtime 10.0.14 otherwise changes `Microsoft.NET.Sdk.WebAssembly.Pack` from 10.0.13 to 10.0.14 and locked restore fails.

### Fixed
- Setup steps: a process tree the operating system refuses to stop after cancellation now reports a failed step (`could not stop '<exe>' after cancellation`) instead of a silent cancellation. The kill failure was previously dropped.
- Setup steps: a refusal that `Process.Kill(true)` reports as an `AggregateException` is reported as the same failed step. Its output drain now runs on the cancellation path too, which the aggregate previously skipped.
- Setup steps: a cancelled step waits at most 2 seconds for its output pipes instead of waiting without bound. A refused kill is reported as a failed step; a kill that succeeds still ends the step as a cancellation. The normal exit path is unchanged.
- Native TextBox placeholder: the unused `PART_Watermark` style is removed. The placeholder already draws the TextSecondary token through the Fluent `TextControlPlaceholderForeground` keys, and a headless test now pins that rendered colour in both themes.

- The API image build no longer fails while `deploy/omp-deps.lock.json` is still pending. `bun pm ls` exits when the install wrote no lockfile, and that happened before the pending check.
- GitHub-hosted CI keeps roll-forward on inside the private SDK 10.0.402. Disabling it stopped the OpenAPI document tool, which targets runtime 10.0.0, from using the installed 10.0.13.
- The web head pins runtime patch 10.0.13. The Blazor WASM SDK otherwise rolls `Microsoft.NET.Sdk.WebAssembly.Pack` to the latest feed patch and locked restore fails.
- Direct-run tool approvals: the hosted adapter denied every request and no tool was ever grantable, so every `allow` answered 403; the new `Harness:Acp:Approval` options set the kinds that reach the endpoints and the tools an allow may grant.
- First load moved focus to the heading and skipped the skip link (WCAG 2.4.1); focus now moves to the heading only when the page changes after the first render (`RouteFocus`).
- Markdown from an agent could carry attributes and media links through the generic-attributes and media-link extensions; the renderer no longer enables them and judges every link it renders, with no limit on their number.
- A refused sign-in for rate limiting looked like a wrong token; the connect screen now says to wait. The connect and auth-config requests end after 15 seconds instead of waiting for ever, and a late refusal of an old token no longer ends a newer session.
- Request timeouts ran before the exception handler, so a timed-out request could be reported as a client cancellation; they now run after it and still answer a 504 problem.
- The route announcer said nothing, or the previous title, after a tab, an agent in the rail or Settings: the browser reports the new address after the render that shows the new title. The workbench now arms the announcer before it changes the view.
- Scrollable code, tool output and file-edit text took keyboard focus without a name or a role; they are now named groups. Kanban card titles, the conversation title and the slash palette wrap instead of being clamped or cut with an ellipsis, so wider text spacing hides none of their words (WCAG 1.4.12). The composer card draws the same two-pixel focus ring as every control.
- After every turn of a real omp session the transcript showed a disclosure "Unsupported content (ContextUsageEvent)": omp reports the context window use at the end of each turn, the API stored it under the name of its CLR type and the web head rendered every unknown kind. The event is now stored as `context_usage` with its two numbers and the transcript does not render it. Found by the local run against the real omp, not by a test.
- Choosing an agent in the rail called the focus before the render that shows its thread, so the focus landed on the thread about to be replaced and fell to the page; it now follows the render. The "Jump to latest" control was removed while it had the keyboard focus (focusing it scrolled the view to the end), which dropped the focus to the page and ended the Tab walk before the composer and the footer; it now stays while it has the focus and hands the focus to the transcript when it is used.
- On a short screen the agents drawer was as tall as its content (a fixed box with `height: auto` under `align-self: start`), so the last agents and the Settings button lay below the screen where neither a pointer nor the keyboard could reach them; it now has the height between the top bar and the footer and scrolls inside it.
- axe reported "needs review" for `aria-controls` next to `aria-haspopup`; the model and option chips no longer set `aria-controls`, and the composer textarea no longer sets `aria-haspopup`.
- Under forced colours a primary button was drawn with a highlight fill, and a palette whose highlight is not opaque made the browser draw a canvas-coloured plate behind its label; it now takes a thicker highlight border and no fill. Agent titles in the rail wrap instead of being cut with an ellipsis.
- The scripted accessibility checks had false findings of their own, found by their first runs and removed: visually hidden live regions read as clipped text, a card around a field was not accepted as its focus ring and its boundary, a focus scroll under the sticky bar read as an undersized target, a control that exists in one walk direction only read as a different Shift+Tab path, Back and Forward assumed an agent row, and the size budget read a comment as an import.
- A theme chosen in the theme switch before the saved theme had finished loading was overwritten by the saved one, so the switch showed the old theme as pressed while the new one had been saved; the person's choice now wins. Found by the full gate on a loaded host, as a failing test.
- The close control of the selected agent row covered the end of its relative time ("ago") when the title was short, and a click on that part of the time reached the control; the title line of the selected row now leaves the room of the control. Found by looking at a screenshot of the real bundle under forced colours.
- The scripted accessibility checks give a state one more attempt from the start page before they call it unreachable, and report a second-attempt success as a warning; a navigation lost on a loaded host stopped a whole group once.
- Two gates or test runs on one host removed each other's throwaway TimescaleDB and Dragonfly containers: `scripts/test-services.sh up` began with `down` on fixed container names, so the second run deleted the first run's containers in the middle of its tests and every test that needs a server failed with a refused connection (67 of 69 failures of a full gate run). `AUTONOGRAMMER_TEST_SERVICES_NAME` now gives a run its own pair (`autonogrammer-test-postgres-<name>`, `autonogrammer-test-dragonfly-<name>`), and `./bootstrap.sh` sets it to `gate-<pid>` unless the caller names one; unset, the script keeps the plain names.
- The remote shell test helper no longer fails when a script that refuses its input has exited before the helper closes the input stream, and the UI tests wait up to ten seconds, not one, for a render that is on its way on a loaded host.
- A Vercel build from the repository of the real web head (`vercel.ts`, no `AUTONOGRAMMER_DEMO`) published the loopback `Api:BaseUrl` of the checked-in `appsettings.json` while the content-security policy allowed only the real origin, so the app could not reach its API. It now runs `scripts/vercel/finish-web-bundle.sh` after the publish, as `scripts/vercel/build-web.sh` does: it writes the origin, refreshes the service-worker asset manifest, verifies every asset against it and refuses a bundle with Demo code.
- The service worker of the real bundle could not install after the API origin was written: it pre-caches `appsettings.json` with the hash in `service-worker-assets.js`, which the rewrite had left stale, so `cache.addAll` rejected and the offline shell and the update notice were lost. `scripts/vercel/refresh-web-manifest.mjs` re-hashes the file, changes the manifest version (the name of the worker's cache) and the first line of `service-worker.js`, and has a `--verify` mode. It replaces only those literals, so the SDK's own spelling of the manifest (`\u002B` for `+` in some SDK versions) stays as published.
- The SBOM freshness check failed on a runner that restores only `Autonogrammer.Linux.slnf`, because the NuGet cache then lacks Android-only packages and their licences became `NOASSERTION`; when the cache has no nuspec for a package version, its licence is now taken from the committed SBOM for the same package name and version, never carried to another version, and a nuspec that is present always wins (even one that declares no licence), so a licence a package stops declaring is still reported.
- The OpenAPI breaking-change check raised `RecursionError` on a schema that refers to itself, directly or through another, and passed a response whose body had been removed; it now ends a descent at a schema pair it is already comparing and reports the removed body.
- The API and worker image builds ignored a failed cleanup after the omp snapshot (the commands were joined with `;`); a failure now stops the build.
- The web smoke's in-app route check failed on a loaded host: the address changed but the render had not finished, so the title was still the boot title and the shell controls were absent, and every step failed. A step that lands on the boot shell now restarts the whole sequence once after a reload, so Back and Forward are still checked against a real history, and a second landing is an error rather than a pass with nothing checked.

### Security

- Owner tokens are never stored or logged: the API holds a digest, compares in constant time and throttles failures; the browser keeps the token in memory only.
- The engine environment allow-list, a per-session workspace, a non-dumpable API process, security headers, request timeouts and Kestrel limits narrow what a hosted run exposes; the threat model and its open questions are in `docs/security/hosted-threat-model.md`.
- The API image no longer makes `/app` writable by the engine's user (the application files are read-only for it) and starts with `DOTNET_EnableDiagnostics=0`, which closes the runtime's diagnostics socket.
- A warning at start when `Hosting:ForwardedHeaders:Mode` is `None` outside Development: behind a proxy the API would see the proxy's address for every client, which makes the per-client rate limits and sign-in throttle useless.

## [0.15.0] - 2026-10-28

Cursor completeness, brand and public launch (Phases 51 to 65). No live model turn, no Windows, macOS or Android run and no Railway deploy of the API or of a cloud agent had happened when the release was cut (recorded 2026-10-28); after the release a staged private worker was deployed to Railway and exercised (see the Cloud agents entry below); items marked partial are built and tested but not exercised live. What each feature has behind it (built, wired into a head, proven by a named test or evidence folder) is recorded row by row in `docs/quality/cursor-parity-audit-2026-10-28.md`.

### Added

- White-and-Purple brand: a logo set, wordmark lockups, banner (1280 by 320), social preview (1280 by 640), favicons, desktop and Android icons under `assets/brand/` with a reproducible build script and a check, in the bundled DM Sans, Anybody and Space Mono faces (Phase 51).
- `AGPL-3.0-or-later` licence: `LICENSE`, `NOTICE`, generated `THIRD-PARTY-NOTICES.md`, REUSE data, an SPDX expression in package metadata and a licence check script; an anonymous `GET /source` that names the licence, the source URL and the running revision, with a source link in the web footer and the About dialogs (Phase 51).
- Community health and release files: `SECURITY.md`, `SUPPORT.md`, `CODE_OF_CONDUCT.md`, issue forms, a pull request template, `CODEOWNERS`, Dependabot, labels, release notes categories, a release workflow that reads this changelog, and a changelog check script (Phase 52).
- Web head foundation: tokens, self-hosted woff2 fonts, theme boot, a responsive shell with top bar, agent rail and footer, no Bootstrap (Phase 53).
- API surface for the web screens: catalogue, board, user event stream, stages, context, steering, config options, commands, change review, session lifecycle and workspace routes (contract version 1.3.0) (Phase 53).
- Web screens: agent rail, thread, composer with model, mode and thinking chips and a slash palette, tool approvals, Changes bar and review, Kanban board, Settings dialog and a terminal view (Phases 54 and 55). A polished White-and-Purple pass fixed the Kanban scroll, the duplicate workspace label, the approval card, the slash palette and the phone top bar (Wave 5). A smoke of 68 axe runs (Light and Dark at 1440, 1024 and 390 px across every screen) found 0 violations in the Wave 5 tree (`scripts/web-smoke/smoke-demo.mjs`).
- Web Settings with 18 pages (General, Runtime, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP, Plugins, Workspaces, Remote environments, Cloud agents, Privacy, Network, Keyboard): Models is a real page over `GET /providers` with the provider directory by region and kind, plan cards with the rule tick box and a write-only key field; Runtime, Rules, Skills, Commands, Hooks, MCP and Plugins are read-only lists over API routes; Agents, Run mode, Workspaces, Remote environments and Cloud agents are information pages that point to the desktop app (partial: Display, Worktrees and Ignore are desktop pages only).
- The web Demo is deployed at https://ompos-web.vercel.app (recorded 2026-10-28): a static build with a simulated engine, no backend and no secret, labelled on screen. `scripts/web-demo/capture.mjs` captured it with headless Chrome from the live URL with no console error and no failed request; screenshots and the method are in `docs/runbooks/evidence/web-app-2026-10-28/`. The Demo answers the provider directory (103 rows), the settings routes and the custom-endpoint rules in the browser (`Demo/DemoProviders*`, `DemoWorkspaceSettings`).
- Desktop screenshots of the real app on Linux (Light theme, Xvfb, no model turn) in `docs/runbooks/evidence/desktop-app-2026-10-28/`, with the two defects the smoke found and fixed: a clipped environment picker header and a Run mode page that opened scrolled down.
- All 92 Cursor slash tokens plus OmpOS's `/update-settings` (93 rows: Built 36, Partial 24, Delegated 9, Unavailable 24), with handler kinds, statuses and reasons, a resolver that merges engine commands, custom command files, skills and subagents, discovery over six directories at two scopes, and a generated `docs/reference/slash-commands.md` (Phase 56). Rewind, fork, side chat, worktree commands, copy, about, workspace sets, `/rules`, `/skills`, `/commands`, `/plugins` and `/run-everything` run as native actions in the desktop window (Wave 3).
- Built-in skills: ten skills (`create-hook`, `create-rule`, `create-skill`, `create-subagent`, `migrate-to-skills`, `review`, `review-security`, `split-to-prs`, `agent-review`, `update-settings`) under `assets/skills/`, discovered by the desktop slash palette and Skills page with scope Built-in; a project skill of the same name takes over (`BuiltinSkillsDesktopTests`). API-hosted sessions carry none.
- Cursor-compatible hooks: a 21-event catalogue, `hooks.json` loaders, a dispatcher, a shell hook runner, per-version consent, a loopback bridge, an omp extension that enforces blocking events (76 node tests pass), an ACP tap, a hooks service and a Hooks page (Phase 57). The desktop head starts one hooks session per omp session and passes the gate to the engine: a hook deny rejects a tool call before the approval policy, the run mode and the user (`EnginePermissionOrderTests`). Cloud workers run consented project hooks and send `_ompos/hook` notifications (`CloudHooksWorkerTests`; the API does not yet mint the consent claim, so cloud sessions report `untrusted`). Partial: no live omp session ran a hook.
- Remote environments over `ssh`: profiles, `ssh -G` resolution, preflight diagnosis, launch rewrite, reconnect, remote file system, remote convention discovery, remote terminals, a profile store, a Remote environments page and a New session target picker (Phase 58), wired into the desktop session path (`RemoteTransportSelectionTests`). A live run of a real `omp acp` through a loopback `sshd` passed on Linux (2026-10-28, `docs/runbooks/evidence/remote-ssh-live-2026-10-28/README.md`). Partial: no prompt turn, no host over a real network; the remote file explorer, terminal pane, hook files and mention files are not wired for ssh sessions.
- Container and WSL environments: `podman exec` into a running container and `wsl.exe` profiles, listing of running containers, a container and WSL form in the Remote environments page and sessions started in them (Wave 6; `ContainerSessionSelectionTests`, `RuntimeEnvironmentsViewTests`). A live podman test against an alpine image passed on Linux and found two defects that are fixed: the dev-container search filter lacked `label=`, and BusyBox `realpath` made every file creation in the container fail (`RemoteScriptsBusyBoxTests`). WSL never ran (Windows only); the terminal pane and the dev-container attach button are not wired.
- Multi-repository workspaces: validated workspace sets, omp launched with every root (`--add-dir`), one git worktree per git root, a multi-root explorer and `.code-workspace` import and export (Phase 59); a workspace sets dialog and `/save-workspace`, `/load-workspace` in the desktop head (partial; no live run).
- Cloud agents on Railway: an agent worker with a JWT-authenticated WebSocket bridge to `omp acp`, a Railway GraphQL client, a static pool client, the API control plane (create, prompt, stop, wake, delete, secrets, limits, pull requests) with a PostgreSQL store, and a cloud agents list and form in the heads (Phase 60), and the desktop `ApiCloudAgentsHost` behind them (`ApiCloudAgentsHostTests`, fakes only; it exists only when `OMPOS_API_BASE_URL` is set). The worker image builds and runs under podman: `scripts/cloud/worker-live-check.mjs` passed 14 of 14 checks (probes, the JWT gate, a real omp 18.12.5 answering `initialize` and `session/new` through the bridge; 2026-10-28, `docs/runbooks/evidence/cloud-worker-container-2026-10-28/README.md`). Partial: the control plane is tested against fakes, `Cloud:Enabled` is false by default, the worker clone bootstrap is missing, and the API is not deployed (no identity provider, OD-03). After the release, on 2026-10-28, a staged private worker (`ompos-worker` in the Railway project `ompos-staging`, no domain, no TCP proxy) was deployed from the released tree, passed Railway's `/healthz` check, and a real `omp acp` answered `initialize` and `session/new` through its JWT-gated bridge from inside the container; no model turn, no clone, no sleep and wake test (`docs/runbooks/evidence/railway-ompos-worker-2026-10-28.md`). The refusal of 2026-10-21 (`Deploys have been paused temporarily`) did not recur that day.
- Provider catalogue of 103 EU, US, CN, global and local providers, aggregators, coding plans and local runtimes as JSON data, a writer for every omp API style, region and kind filters, coding-plan rule cards, `GET /providers` and credential routes, and a generated `docs/reference/providers.md` (Phase 62). A read-only experiment with omp 18.12.6 showed omp accepts a `models.yml` with a block for every route of the catalogue (2026-10-27). Partial: no row was called with a real credential (OQ-71).
- Run modes (Auto-review, Allowlist, Run Everything), `permissions.json`, `sandbox.json` readers and writers, a decision engine and a Run mode page (Phase 63); the desktop head applies them to every session (`--approval-mode`, a `bash:` overlay and the permission order above). Partial; Auto-review's hosted classifier is not available; a changed run mode reaches omp only when the engine is composed again.
- `@` mentions with a fuzzy picker, Cursor-style ignore files (gitignore syntax) for the agent, Tab and mentions, a conversation search index, a context ring and ACP prompt content blocks (Phase 63); a mention reaches the prompt as a resource and `usage_update` feeds the context ring (`AgentSessionRuntimeWiringTests`). Partial: no live turn; remote files are not mentionable.
- MCP configuration import from OmpOS, Cursor, Claude Code and VS Code files, a server catalog and an MCP settings page (Phase 63), plus 13 vendor presets added from the page (written disabled, no credential; `McpPresetSourceTests`) (Wave 6). Partial: no live omp session called an MCP tool.
- Image attachments in the desktop composer: paste, pick or drop, sent as ACP image blocks when omp's `initialize` reply declares `promptCapabilities.image` (`AcpImagePromptTests`, `AgentSessionRuntimeImageTests`). Partial: no live turn; model vision stays Unknown; no web or Android attach.
- File checkpoints with rewind and restore, a worktree command model, a message queue, custom modes, side chats and fork (Phase 63), mounted in the desktop window (`ShellPaneToolsTests`; fork over `session/fork`, `AcpForkTests`). Partial: rewind restores files only; no live omp session.
- Automations: a cron scheduler, local and cloud targets, a first-run consent card and an Automations page in the desktop Settings (the 22nd page), and API routes with `Automations:Enabled` false by default (contract version 1.5.0; `AutomationsHostTests`, `LocalSessionAutomationTargetTests`, 73 API tests). Webhook triggers are refused on the desktop. Partial: the real UI session path is not tested; no live run.
- Documentation in GitBook's Git Sync layout: `.gitbook.yaml`, `docs/SUMMARY.md`, guides for getting started, the desktop and web apps, concepts, providers, settings and troubleshooting, an architecture diagram, an HTTP API reference, a publish runbook, a documentation check script (`scripts/ci/check-docs.sh`) and a refreshed Cursor parity audit (`docs/quality/cursor-parity-audit-2026-10-28.md`, re-graded against the final tree) (Phase 64). The README shows eight screenshots and links the Demo.
- GitHub settings applied with `scripts/github/apply-repo-settings.sh` (2026-10-28): description, homepage (the Demo URL), 15 topics, discussions on, wiki off, delete branch on merge, private vulnerability reporting, Dependabot security updates, secret scanning with push protection and 22 labels. The social preview upload, the discussion categories and the GitBook space need the owner.

### Changed

- The Light theme is the default in the desktop and web heads; Dark and System remain choices (Phase 51).
- The README is rewritten as the GitHub landing with a feature status table and the licence block (Phase 64).
- `docs/architecture.md` now covers the web shell, remote environments, cloud agents and hooks; `docs/handoff.md` is marked as the historical v0.9.1 handoff (Phase 64).
- ADR-0013 decision 4 (no shell, closed stdin for plugin hooks) is amended by ADR-0016 so that Cursor hook commands run as written (Phase 57).
- Licence statements in `docs/plugins.md`, ADR-0013, the GSD-Core plugin README and the deferred-and-blocked record now say OmpOS is `AGPL-3.0-or-later`; the licences of `agent-swarm` and `agent-substrate` stay open (Phase 64).
- The README shows eight screenshots (six of the Demo build with a simulated engine, two of the desktop app on Linux without a model turn) and links the deployed Demo; the homepage of the repository is the Demo URL until the documentation site exists (Phase 64).
- The Settings window has 21 pages plus Automations when the head supplies it; `docs/settings-parity.md` tracks the 50 further rows S01 to S50 by id (Phase 61).

### Fixed

- A hook run log is created with owner-only permissions because it holds prompts and output excerpts (Phase 57).
- The web head no longer ships Bootstrap; the unused library and its annotation are removed (Phase 53).
- The desktop environment picker's header ("Where should the agent run?") was clipped in the rail and hid the current choice; it wraps now (2026-10-28 smoke).
- The desktop Run mode page opened scrolled down because the selected item scrolled the page (`AutoScrollToSelectedItem="False"`).
- Container sessions: the dev container search filter lacked `label=`, and BusyBox `realpath` (alpine) refused `--` and non-existent paths, so every file creation in the container failed; found by the live podman test, fixed with a fallback (`RemoteScriptsBusyBoxTests`).
- The web Kanban board showed one card at the right edge after a tab switch (the page kept the thread's scroll offset); the board resets the scroll and each lane scrolls inside itself.
- A whitespace-only ignore pattern is rejected as a problem instead of being stored (`IgnoreSettingsLimits`).
- Automations API responses no longer carry null members.

### Security

- AGPL section 13 source offer for network use, anonymous and without sign-in (Phase 51).
- Cloud agent worker tokens are short-lived and verified by the worker; secrets are write-only through the API (Phase 60).
- Provider key writes, custom endpoints and live tests are off by default in the API and need explicit operator switches; the live test connects to public addresses only (Phase 62).
- The web provider key field is write-only (`type=password`, never rendered with a value). In the Demo it is disabled and the Demo handler answers every credential write with 403 `credential_writes_disabled`, so nothing typed there is stored or sent.

## [0.14.0] - 2026-10-27

Agent loop on the omp engine and the default Cursor-style Agent Panel (Phases 44 to 50).

### Added

- `claude-ultrathink`, `agent-swarm` and `agent-substrate` as pinned plugin submodules, and `plugins/gsd-core-omp`, an omp plugin whose node test suite is part of `bootstrap.sh` and CI (Phase 44).
- A launch contract that starts every omp process the application runs (the engine session and each omp terminal) with the four plugins loaded once, with a connection report and fix text when a plugin is missing (Phases 44 and 48).
- Agent loop stage sources: stage events and task context in a session catalogue that survives a restart, and visible degradation when a plugin is missing or failing (Phase 45).
- The default Agent Panel: a session rail, thread, composer with model, mode and thinking chips, several concurrent sessions and a pipeline strip (Phase 46).
- The omp slash command palette in the composer, listing the commands omp advertises (Phase 49).
- A Changes bar with Keep, Undo, Review, Keep all and Undo all, feeding the Diff review tool (Phase 49).
- Live steering: a running session takes a steering message from the composer or its Kanban card through omp's `_session/steering`, with truthful delivery reporting (Phase 49).
- Cursor-style visual parity: static Greptile typefaces (DM Sans, Anybody, Space Mono), a token set and control library, 32 px targets, compact density, and screenshots in Dark and Light (Phase 50).

### Changed

- The Agent Panel is the single default surface: the idea of a second agent-loop surface was superseded on 2026-10-26.
- The Agent Panel on the web head (Phase 47) stays deferred.

### Fixed

- An editor tab-switch deadlock in the TextMate integration (Phase 50).
- The GSD-Core plugin's `bin` scripts, which `.gitignore` hid, are now tracked (Phase 44).

## [0.13.0] - 2026-10-27

Cursor 1:1 workspace and provider platform (Phases 36 to 43).

### Added

- The shared contracts every surface consumes: one token set, command record, status model, provider record, secret type, and versioned herdr and omp adapter contracts with recorded fakes (Phase 36).
- Session replay without duplicates and a real Linux local PTY terminal backend (Phase 37).
- A slash command registry and chat window states, with the catalogue checked against a recorded Cursor reference (Phase 38).
- A Kanban board with one card per chat, live status and event-driven changes, and an Agents tab (Phase 39).
- A Cursor-shaped Settings window with eleven pages, including Models, Keyboard Shortcuts and Network, over omp's `models.yml` (Phase 40).
- An omp configuration library and an owner-only provider key store (Phase 40).
- A model provider catalogue in which a provider is a registry entry, with error mapping for quota exhaustion and plan expiry (Phase 40).
- A composer model picker over omp's ACP model option (Phase 40).
- A Pets companion state model, preferences and text equivalents (Phase 41).
- A cloud session host over a fake Railway environment client, as a library (Phase 42).

### Changed

- The static Greptile faces of Phase 50 (0.14.0) replaced the variable font files bundled in Phase 36.
- No product project references the herdr adapter yet; sessions re-attach through the session registry. A Pets view, live cloud runs and mobile and web terminals (Phase 43) are partial or deferred in the parity audit.

## [0.12.0] - 2026-10-27

Cursor parity and quality bar (Phases 31 to 35).

### Added

- Plugin manifests with API version, capabilities and permissions, a host that loads, quarantines or rejects plugins without crashing, and a hook runner that needs consent for the exact command (Phase 31).
- A hooks view with enable, disable, reorder and explicit consent (Phase 31).
- Automations guards and engine (chain depth, fail-closed store, rate and runtime limits) and a presentation model (Phase 32).
- An omp terminal launcher, a shared plugin and MCP registry, and a host-owned MCP gateway with grant enforcement (Phase 33).
- A documented suppression audit and an automated web smoke with axe-core (Phase 35).
- A Cursor parity audit of 221 rows (98 met, 16 met live, 70 partial, 20 deferred, 3 not started, 14 omitted by decision), recorded at commit `d75fd88` (Phase 35).

### Changed

- Phase 34 (remote development) stayed blocked: no verified remote omp endpoint exists (OD-06), and no stub ships.
- No head loads a plugin manifest, runs a hook or composes the automations engine yet (OQ-84).
- Decision records exist for plugin isolation and the MCP client (Phase 35).

### Security

- A plugin quarantine fails closed and lifts only with a recorded reason (Phase 31).
- One pinned, digest-checked secret scan runs in `bootstrap.sh` and CI, with a fingerprint allow-list for fake test tokens (Phase 35).

## [0.11.0] - 2026-10-22

ADE harness (Phases 26 to 30).

### Added

- Side chats with transcript search, and an Agents window with a status filter (Phase 26).
- A per-run git worktree manager and opt-in run worktrees (Phase 26).
- A hosted run contract with run endpoints (Phase 26).
- An engine-side permission resolver for in-process ACP approvals (Phase 26).
- A code editor on AvaloniaEdit, a workspace tree, per-hunk diff review and inline edit logic (Phase 27).
- An integrated terminal panel over a PTY runner (Phase 28).
- Project rules, skills and modes, codebase search and export logic (Phases 28 and 29).

### Changed

- The native shell wires the diff, inline edit, explorer, rules, MCP tools, search, completion, export, editor and terminal views.
- Phase 30 (VS Code extension compatibility, admin and team controls) is deferred in writing until its criteria hold.

### Fixed

- The sandbox runner freezes the process tree before killing it.

### Security

- Session ownership is enforced on every session endpoint, with remote control authentication tests (Phase 26).

## [0.10.0] - 2026-10-26

Cross-platform UI on Avalonia (Phases 20 to 25.9).

### Added

- A shared Avalonia native shell with a framework-neutral presentation layer, an accessibility baseline and plugin host contracts (Phase 20).
- Session chat, tool approvals and steering on the shared presentation layer, with approvals for edit, delete, move, execute, read and search (Phase 21).
- The Blazor WebAssembly web head on the shared presentation layer, with an axe-core smoke in `bootstrap.sh` and CI (Phase 23).
- An Android head, `apps/OmpOS.Android`, built for `net10.0-android36.0` with warnings as errors, and a CI job that builds it (Phase 24).
- 13 Avalonia parity behaviours that were unrendered, each with a native test (Phase 25).
- A codebase quality audit register of 130 findings and eight fix phases (Phases 25.1 to 25.9).
- A vulnerable-package audit that also covers the Android head (Phase 24).

### Changed

- Every CI job runs on the Omega CI runner pool, MSBuild is sized to the runner's 2 vCPU budget, and two test modules run at a time.
- Phase 22 (Linux, Windows and macOS desktop heads) was skipped by owner decision on 2026-10-24: the X11 and XWayland starts are recorded; the Windows and Mac runners are external.
- iOS and macOS builds stay blocked on a Mac channel.

### Removed

- The MAUI head (`apps/OmpOS.App`), after every one of its parity rows had a passing Avalonia test (Phase 25).

### Fixed

- The web head sends prompts and streams only to the configured API origin, and its SSE client stays bounded under abuse (Phase 25.2).
- Every read of external bytes has a named cap and fails with a bounded error when oversized (Phase 25.3).
- Failures that were swallowed are logged or surfaced to the caller (Phase 25.5).
- The UI heads honour reduced motion, label images and keep the error dismiss keyboard-operable (Phase 25.7).
- Tests assert what their names claim, and settlement tests wait deterministically instead of sleeping (Phase 25.8).
- Every suppression and skip marker has a recorded decision (Phase 25.9).

### Security

- Build and deploy inputs are verified or pinned: installers checked before execution, CI actions and runners pinned, images pinned by digest (Phase 25.4).
- Bearer tokens reach only https or loopback origins, compose files in shared temporary directories are owner-only, and repository setup steps need per-repository consent (Phase 25.6).

## [0.9.2] - 2026-10-21

Hardening and hosted API (Phases 1 to 19 of the 0.9.2 roadmap; audit status `tech_debt`).

### Added

- A PostgreSQL session log shared by the API and the worker, read back gapless across processes (Phase 7).
- Permission requests, parked ACP approvals and a session approval service, with decisions routed to the pending request (Phase 10).
- An OIDC bearer boundary for the API.
- Verified agentOS network policy with fail-closed vendor operations (Phase 4).
- A per-sandbox podman secret credential, revoked at teardown (Phase 6; other providers stay blocked).
- Podman build files for the API image and a pinned omp runtime inside it.
- Frozen contract fixtures of omp 18.12.5 `session/update` variants, from a bounded live capture (Phase 11).
- A build-time content-security-policy `connect-src` from one validated API origin, an icon-only send control and the skip link first in tab order.
- Readiness, exporter, prompt logging and rate-limit seams.
- A throwaway PostgreSQL service for the CI test runs.

### Changed

- Session work runs in the API on Railway, replacing Temporal; the Temporal worker and its replay phase were superseded (Phases 8 and 9).
- The MAUI-specific phases were superseded by the Avalonia program of 0.10.0 (Phases 15 and 16).
- Phases 6, 12, 13 and 14 stay blocked on external criteria (paused Railway deploys, an unset production API origin and no verified vendor secret-store operation).

### Fixed

- Correlation ids, resume positions, owner ids, container ids and session ids are validated.
- SSE ends on id-less or duplicate terminal frames.
- Per-sandbox credential and host-secret flags are enforced.
- The SDK archive digest is verified before install, and the CI workflows profile boots.
- Unregistered permission requests settle as denied at the registration bound.

### Security

- Production refuses the in-memory adapter and the in-memory session store, and the API verifies bearer tokens at its boundary.

### Removed

- The Temporal worker and its Temporal Cloud options (superseded 2026-10-21).

## [0.9.1] - 2026-10-21

Foundation through verified handoff (Phases 1 to 5).

### Added

- A governed 15-project .NET 10 monorepo with analyzers, warnings as errors, deterministic builds, central package management with committed lock files, and `bootstrap.sh` as the single gate command (Phase 1).
- `OmpOS.Contracts`, the shared contract surface, with kind converters and a source-generated JSON context (Phase 1).
- Layer-direction architecture tests with live-gate proofs, and a CI pipeline that mirrors `bootstrap.sh` (Phase 1).
- A validated podman compose stack and an eight-file documentation skeleton with the first architecture decision records (Phase 1).
- The catch-all prompt parser, ACP v1 and omp RPC clients, an in-memory adapter, session orchestration and a Server-Sent Events API (Phase 2).
- A sandbox provider abstraction with fake and local podman-compose providers, and durable workflows through a direct runner (Phase 3).
- A shared Razor class library chat surface with a Generative UI renderer, a Blazor WebAssembly web head and a MAUI Blazor Hybrid head (Phase 4).
- A clean-clone bootstrap record, the handoff document and the open-questions register (Phase 5).

[Unreleased]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.18...HEAD
[0.20.18]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.17...v0.20.18
[0.20.19]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.18...v0.20.19
[0.20.20]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.19...v0.20.20
[0.20.21]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.20...v0.20.21
[0.20.17]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.16...v0.20.17
[0.20.16]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.15...v0.20.16
[0.20.15]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.14...v0.20.15
[0.20.14]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.13...v0.20.14
[0.20.13]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.12...v0.20.13
[0.20.12]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.11...v0.20.12
[0.20.11]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.10...v0.20.11
[0.20.10]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.9...v0.20.10
[0.20.9]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.8...v0.20.9
[0.20.8]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.7...v0.20.8
[0.20.7]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.6...v0.20.7
[0.20.6]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.5...v0.20.6
[0.20.5]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.4...v0.20.5
[0.20.4]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.3...v0.20.4
[0.20.3]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.2...v0.20.3
[0.20.2]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.1...v0.20.2
[0.20.1]: https://github.com/swcstudiospace/OmpOS/compare/v0.20.0...v0.20.1
[0.20.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.19.2...v0.20.0
[0.19.2]: https://github.com/swcstudiospace/OmpOS/compare/v0.19.1...v0.19.2
[0.19.1]: https://github.com/swcstudiospace/OmpOS/compare/v0.19.0...v0.19.1
[0.19.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.18.0...v0.19.0
[0.18.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.17.0...v0.18.0
[0.17.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.16.3...v0.17.0
[0.16.3]: https://github.com/swcstudiospace/OmpOS/compare/v0.16.2...v0.16.3
[0.16.2]: https://github.com/swcstudiospace/OmpOS/compare/v0.16.1...v0.16.2
[0.16.1]: https://github.com/swcstudiospace/OmpOS/compare/v0.16.0...v0.16.1
[0.16.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.15.0...v0.16.0
[0.15.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.14.0...v0.15.0
[0.14.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/swcstudiospace/OmpOS/compare/v0.9.2...v0.10.0
[0.9.2]: https://github.com/swcstudiospace/OmpOS/compare/v0.9.1...v0.9.2
[0.9.1]: https://github.com/swcstudiospace/OmpOS/releases/tag/v0.9.1
