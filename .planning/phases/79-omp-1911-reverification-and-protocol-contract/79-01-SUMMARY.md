---
phase: 79-omp-1911-reverification-and-protocol-contract
plan: "01"
subsystem: harness-rpc, harness-acp, protocol-evidence
tags: [omp-19.1.1, rpc-handshake, acp-initialize, fixtures, no-live-turn]
dependency_graph:
  requires: [OMP19-protocol-evidence-v1 (A07 contract), ADR-0038]
  provides: [omp-19-reverification-ledger, rpc-19.1.1-handshake-fixture, acp-19.1.1-initialize-fixture]
  affects: [80-rpc-production-wiring]
tech_stack:
  added: []
  patterns: [observed-19.1.1 evidence labels, manifest sidecars (OMP19-protocol-evidence v1.0.0), handshake-only probe in isolated HOME]
key_files:
  created:
    - docs/architecture/omp-19-reverification.md
    - docs/runbooks/evidence/omp-19.1.1-protocol-2026-11-23/README.md
    - tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-observed-19.1.1-20261123T202249Z.ndjson
    - tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-observed-19.1.1-20261123T202249Z.manifest.json
    - tests/Autonogrammer.Harness.Acp.Tests/TestData/frames/acp-observed-19.1.1-20261123T202251Z.ndjson
    - tests/Autonogrammer.Harness.Acp.Tests/TestData/frames/acp-observed-19.1.1-20261123T202251Z.manifest.json
    - tests/Autonogrammer.Harness.Rpc.Tests/Omp19HandshakeContractTests.cs
    - tests/Autonogrammer.Harness.Acp.Tests/Acp19HandshakeContractTests.cs
  modified:
    - tests/Autonogrammer.Harness.Rpc.Tests/Autonogrammer.Harness.Rpc.Tests.csproj (copy *.manifest.json to output)
decisions:
  - "Version pin stays 18.12.x in code; the 19.1.1 handshake-compatibility evidence is recorded in the ledger and the pin moves in Phase 80 with the prompt-path fixtures (79-CONTEXT D-03)"
  - "No production source file changed (plan Task 3): the two additive 19.1.1 fields are already tolerated by the parsers, proven by tests"
  - "RPC probe passed --model opus because a fresh profile has no default model and 19.1.1 exits 1 before ready without one; model selection is not a model turn"
metrics:
  tasks: 3
  status: complete
---

# Phase 79 Plan 01: omp 19.1.1 Re-verification and Protocol Contract Summary

**One-liner:** The installed omp 19.1.1 RPC/ACP handshake surface was probed handshake-only (no prompt, fresh `HOME`, credentials scrubbed), recorded in `docs/architecture/omp-19-reverification.md` with every fact labeled observed-19.1.1 or verified-18.12.6, and frozen as two manifest-carrying captures with 7 new contract tests — drift is two additive fields plus additive `get_state` keys; caps, negotiation, auth method, flags and envelopes are unchanged.

## Tasks Executed

**Task 1 (auto): RPC and ACP handshake contract from observed output.** Probe of `~/.local/bin/omp` (reports `omp/19.1.1`): RPC `--mode rpc --no-ui --cwd <tmp> --session-dir <tmp> --no-session --model opus` returned `ready` (protocolVersion 1, supported [1,2], 1 MiB/64 MiB caps, **new** `supportsBashExcludeFromContext: true`), confirmed `negotiate_protocol` v2, answered `get_state` and rejected `nope` in the 18.12.x envelope, exit 0 on EOF. ACP `omp acp` answered one `initialize` (protocolVersion 1, authMethods `[agent]`, same capability groups, agentInfo version 19.1.1, **new** `_meta.steering.supported: true`) at 1322 ms in a fresh HOME, exit 0. No frame exceeded 1 MiB, so no `rpc_chunk` was emitted and the 256 KiB chunk-payload cap stays verified-18.12.6. Evidence: `docs/runbooks/evidence/omp-19.1.1-protocol-2026-11-23/README.md`.

**Task 2 (auto): Command-surface delta, launch flags, version pin/probe.** Ledger command table covers every client-sent command: `negotiate_protocol`, `get_state` and the unknown probe observed-19.1.1; `prompt` (incl. steer), `abort`, `abort_and_restore_queue`, `switch_session` unobservable-without-model-turn and recorded as verified-18.12.6 shapes. `get_state` gained `configuredThinkingLevel`, `thinkingLevels`, `slowModeScope`; the missing `sessionFile` is confounded by the probe's `--no-session`, not claimed as removal. `--mode rpc`/`--no-ui`/`--cwd` accepted verbatim in the live handshake and documented in 19.1.1 `--help`. Pin decision recorded: `OmpVersion.Parse` handles `omp/19.1.1`, but the `18.12.x` pin deliberately does not move in Phase 79 (evidence now, pin + prompt fixtures in Phase 80). New observation recorded: in a fresh profile with no default model, 19.1.1 exits 1 before `ready` with "No default model selected." — `OmpRpcProcess` passes no `--model`, disposition left to Phase 80, not claimed as drift.

**Task 3 (auto): Fixture freeze and current-wiring confirmation.** Two captures frozen under OMP19-protocol-evidence v1.0.0 with sidecar manifests (`live_model_turn: false`, line hashes, R1/R4 redactions recorded; `get_state` systemPrompt/dumpTools redacted under R4). `Omp19HandshakeContractTests` (5 tests: ready caps/new-field drift vs the 18.12.3 fixture, command shapes incl. additive-key drift, full `OmpRpcSession` replay, manifest digest validation V1–V4/V6, version-pin decision) and `Acp19HandshakeContractTests` (2 tests: initialize parse incl. `_meta` drift, manifest digests). Rpc test csproj widened to copy `*.manifest.json` to output (the widening the A07 contract anticipates). Wiring confirmed unchanged with commands in the ledger: 8 production projects reference Harness.Acp and none references Harness.Rpc; Rpc type grep over `src apps services` outside the Rpc project is empty (AC-302, AC-303: production Rpc consumer count 0, tests only). Zero production source files changed.

## OQ-520..525 dispositions (for A15/A01 to apply — this plan did not edit docs/open-questions.md)

- **OQ-520 (ready frame): close** — observed-19.1.1, ledger R-READY, frozen capture seq 1.
- **OQ-521 (negotiate v2): close** — v2 exists and confirms as on 18.12.x; what v2 changes over v1 (chunking) is unchanged at the caps level recorded in R-CAPS.
- **OQ-522 (caps): close the frame/reassembled caps** (1 MiB / 64 MiB observed-19.1.1, match coded constants); **chunk-payload cap stays open** as unobservable without a >1 MiB frame.
- **OQ-523 (command delta): table delivered**; prompt-path rows stay verified-18.12.6 to Phase 80 by the row's own terms.
- **OQ-524 (flags + pin): close the flag half** (accepted live + in `--help`); pin half resolved as a recorded decision — pin moves in Phase 80 citing this ledger.
- **OQ-525 (RPC approvals): stays open** — no permission surface is observable in a handshake; deny-by-default stands.

## Verification Performed

- `dotnet build src/Autonogrammer.Harness.Rpc -c Release` → 0 errors, 0 warnings; same for `src/Autonogrammer.Harness.Acp`
- `dotnet test --project tests/Autonogrammer.Harness.Rpc.Tests -c Release --filter-not-trait "Category=Live"` → **65 passed, 0 failed** (5 new)
- `dotnet test --project tests/Autonogrammer.Harness.Acp.Tests -c Release --filter-not-trait "Category=Live"` → **277 passed, 0 failed** (2 new)
- `sha256sum` over both frozen `.ndjson` files reproduces the manifest `frames_sha256` values (`29da29f0…`, `5864a2c1…`); `grep -c "/root"` over both captures → 0
- Ledger label sweep: every protocol fact row carries observed-19.1.1 or verified-18.12.6 (AC-301)

Targeted verification only, per assignment: no repo-wide format/lint/full-suite run; `./bootstrap.sh` remains the phase gate for A01.

## Deviations

- The plan lists `docs/open-questions.md` under files for Tasks 1–2; the Lane-C assignment reserves that file for other owners, so OQ closures are recorded here for A15/A01 to apply instead of editing it.
- The RPC probe needed `--model opus` beyond the production argv (fresh profile has no default model; exit 1 without it). Production's first four argv elements were exercised verbatim; the addition is recorded in the manifest argv and evidence README.

**Status:** 79-01 complete.
