---
requirements-completed: [VERIFY-01, VERIFY-02, VERIFY-03]
---

# Phase 5 Plan 01: Verify & Handoff docs Summary

One-liner: AC_1..AC_11 handoff report with evidence, open-questions completion
(Q-HARN-UPDATE, Q-AC-LIST), README verified-where + MAUI workload docs, ADR index.

## Files changed

| File | Change |
| ---- | ------ |
| `docs/open-questions.md` | Appended 2 rows: `Q-HARN-UPDATE`, `Q-AC-LIST` (one-line table rows only; no other edit) |
| `docs/handoff.md` | Created: provenance note, bootstrap evidence, AC_1..AC_11 table, explicit deferrals, plan index, open items |
| `README.md` | Reworked “What is verified where” (SHELL-01: Linux verifies API/harness/sandbox/workflows/web/EngineHost; MAUI by inspection only, macOS build human-deferred); added MAUI workload requirement (`OmpOS.Linux.slnf` is the supported Linux path); links `docs/handoff.md` |
| `docs/decisions/README.md` | Created: index of ADR-0001..ADR-0003 with status + in-text amendments |
| `.planning/phases/05-verify/05-01-SUMMARY.md` | This file |

No `.cs`, `.razor`, `.csproj`, `.props`, `.slnx`, `.slnf`, `vercel.json`,
`Directory.Packages.props`, or `.planning/*` (other than this summary) file was
touched. No command was executed (read-only greps/reads only); no commit was made
(git writes forbidden for this task — aiming is left to Main).

## Greps run (before → after)

1. First-party code scope (the plan’s exact sweep):
   `grep -rn 'TODO\|FIXME\|HACK\|XXX' src tests apps services --include='*.cs' --include='*.razor'`
   (excluding `bin/`, `obj/`) → **0 hits before → 0 hits after.**
2. Broad sweep (brief scope, all file types):
   `grep -rln 'TODO\|FIXME\|HACK\|XXX' src tests apps services docs deploy`
   (excluding `bin/`, `obj/`) → **23 files before → 23 files after**, all
   classified non-actionable (no owner-ID comment edits applicable):
   - 21 vendored third-party Bootstrap assets under
     `apps/OmpOS.Web/wwwroot/lib/bootstrap/dist/` (upstream `TODO`/`FIXME`
     comments in `bootstrap.js`/`bootstrap.esm.js`/`bootstrap.bundle.js` and
     base64 `XXX`-like sequences in `.map` files) — not first-party source;
     editing upstream vendored files would be wrong.
   - `tests/OmpOS.Harness.Rpc.Tests/TestData/frames/rpc-out.ndjson` — 5 `TODO`
     tokens, all inside recorded real-omp frame payloads (e.g. a command
     description “Write todos as Markdown to a file (default: TODO.md)” and
     rule-check names); fixture data, not code markers; editing would corrupt
     the replay fixture.
   - `docs/open-questions.md:3` — policy prose (“Code `TODO`s link to these
     IDs”), not a marker.
3. Collision check for the new IDs:
   `grep -rn 'Q-HARN-UPDATE\|Q-HARN-SESSION\|SESSION-UPDATE'` → 0 before;
   `grep -rn 'Q-AC-LIST\|AC-LIST'` (docs + .planning) → 0 rows before (only the
   prose requirement in `05-CONTEXT.md:21`). After: exactly the 2 added rows.
4. `NETSDK1147` provenance: no occurrence anywhere in the repo (docs, planning,
   scripts, CI). The README wording follows the task brief; the workload-absence
   facts behind it are file-backed (`apps/OmpOS.App/OmpOS.App.csproj`: `UseMaui`,
   `net10.0-android;ios;maccatalyst`, “never restores or builds on Linux (no
   workloads)”; `OmpOS.Linux.slnf` excludes the App; `.github/workflows/ci.yml`
   `shells` job runs `dotnet workload install maui` on `macos-15`;
   `.planning/STATE.md:101-102` deferred-human macOS row). The literal
   `NETSDK1147`/`maui-tizen` strings were not re-observed (no dotnet execution
   permitted for this task).

## Open questions

Added (no existing row edited, no status changed):

- `Q-HARN-UPDATE` (owner `platform`, status `open`): which session/update
  variants a real omp turn emits over ACP stdio and omp RPC JSONL; default —
  fixtures frozen from recorded/scripted turns, unseen variants take the
  unknown-fallback path. Complements (not replaces) `OQ-02`; required because
  VERIFY-02 (REQUIREMENTS.md) names this fact and only the Q-style ID was
  missing.
- `Q-AC-LIST` (owner `ompos-maintainers`, status `open`): where the authoritative
  AC_1..AC_11 enumeration lives; default — verify against the reconstructed list
  and state provenance in `docs/handoff.md`. Required because `05-CONTEXT.md:21`
  asserts `docs/open-questions.md` carries this row and the handoff cites it.

VERIFY-02 coverage confirmed for the other seven facts: A2UI channel → OQ-03;
agentOS hosting → OQ-01 + Q-SB-AGENTOS; OpenShell contract → OQ-06; Railway
endpoints/auth → OQ-23 + Q-SB-RAILWAY; dTEE/dstack verifier → OQ-07 +
Q-SB-ENCLAVE; Vercel build image → OQ-09; Podman 5.x → OQ-10.

## AC rows (status + evidence, as written in `docs/handoff.md`)

- AC_1 Implemented — `/tmp/bootstrap-run3.log` exit 0, 481/0.
- AC_2 Implemented — `PromptParserTests`, `MagicKeywordBoundaryTests`,
  `RecognizerOrderingTests`.
- AC_3 Implemented (remote transport stubbed) — `HarnessStateMachineTests`,
  `InMemoryAdapterTests`, `FailureModeTests`; WS stub `WebSocketOmpTransportStubTests`
  + Q-HARN-WS.
- AC_4 Implemented (smoke skips without omp; interactive approval deferred) —
  `AcpFrameReplayTests`, `AcpStdioTransportTests`, `AcpAgentSessionTests`,
  `AcpLiveSmokeTests`, `RpcFrameReplayTests`, `RpcChunkReassemblyTests`,
  `RpcCommandCorrelationTests`, `RpcLiveSmokeTests`; Q-HARN-PERMISSION.
- AC_5 Implemented — `SessionServiceTests`, `ProjectionReplayTests`,
  `SessionRepositoryContractTests`.
- AC_6 Implemented — `SessionEndpointTests`, `SseResumeTests`, `GuardTests`;
  `docs/api/openapi.json`.
- AC_7 Implemented (credential issuance deferred) —
  `FakeSandboxProviderContractTests`, `LocalPodmanComposeProviderContractTests`,
  `SandboxLifecycleTests`, `IsolationDefaultsTests`; `deploy/compose.yaml`;
  Q-SB-CREDENTIALS.
- AC_8 Stubbed (by design) — `VendorStubTests`; Q-SB-AGENTOS, Q-SB-RAILWAY,
  Q-SB-ENCLAVE.
- AC_9 Implemented (composition + direct retry deferred) — `DirectRunnerTests`,
  `SessionRunWorkflowContractTests`, `TemporalReplayTests`, `WorkerHostTests`;
  Q-SB-WORKER-COMPOSE, Q-WF-RETRY.
- AC_10 Implemented (RCL + web) / Deferred (MAUI binaries) —
  `UiBlockRendererTests`, `ReplayTreeTests`, `AccessibilityTests`,
  `SseParserTests`, `PublishSmokeTests`, `VercelConfigTests`, `EngineHostTests`;
  deferred-human macOS `shells` job.
- AC_11 Implemented — bootstrap + format gates; `ArchUnitLayerTests`,
  `ProjectGraphTests`, `RepoHygieneTests`; this handoff.

## Deviations from the plan

1. **Plan task 1 (VERIFY-01 fresh-clone run + `docs/runbooks/local-dev.md`
   section) not executed.** The task brief forbids command execution
   (`./bootstrap.sh` included) and forbids editing `local-dev.md`; the brief
   takes precedence per its conflict rule. The handoff records the existing
   working-tree log (`/tmp/bootstrap-run3.log`, paths under
   `/root/src/repos/ompos`, not a fresh clone) and leaves a dated fresh-clone
   record as open item 1. `local-dev.md:5` already documents the one-command
   clean-clone flow.
2. **Plan tasks 4 (architecture.md) and 5 (STATE.md) not executed.** Both files
   are outside the brief’s named outputs and `.planning/*` edits are forbidden.
3. **Sweep scope widened vs the plan** (all file types, not just
   `--include='*.cs' --include='*.razor'`), per the brief; the extra hits were
   all classified non-actionable above — zero comment edits resulted.
4. **Q-AC-LIST added** although not in VERIFY-02’s list — required by
   `05-CONTEXT.md:21`’s assertion and the handoff citation.
5. **Plan task 3’s subagent-infrastructure note (billing outage) omitted** — no
   verifiable source was available to this task, and unverified claims do not go
   in the handoff. Phase 4’s missing per-plan SUMMARYs (`04-*-SUMMARY.md`
   absent; only `04-REVIEW.md`/`04-VERIFICATION.md` present) recorded instead.
6. **Q-WF-HISTORY observed without a row**: ADR-0003 marks it resolved in-text;
   out of VERIFY-02 scope, left untouched.

## Requirements-completed note

Re-verified at HEAD a26fe2f on 2026-10-21 (`05-VERIFICATION.md`). The re-run passes, so VERIFY-02 stays in `requirements-completed`. All three requirements are VERIFIED:
- VERIFY-01: clean-clone run in `docs/runbooks/local-dev.md` (Clean-clone bootstrap section); log `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21.log` (615 total, 0 failed, `bootstrap: OK`).
- VERIFY-02: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:115`) is enforced; `docs/runbooks/evidence/gate-a26fe2f-2026-10-21.log` shows `OmpOS.ArchitectureTests` passed (lines 78 and 95).
- VERIFY-03: `docs/handoff.md:72-82` holds one row each for AC_1 through AC_11.

