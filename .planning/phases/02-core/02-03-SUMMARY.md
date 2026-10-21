---
requirements-completed: [HARN-04]
---

# Phase 2 · Plan 03 Summary — Real ACP v1 client (HARN-04)

**Completed:** 2026-10-20 · **Agent:** sonic `P2Acp` · **Verified:** orchestrator

## What landed

- `src/OmpOS.Harness.Acp`: source-generated ACP JSON-RPC 2.0 envelopes (`AcpJsonContext`), `AcpStdioTransport` (process `[omp, acp]`, concurrent stderr drain, handshake timeout, process-tree kill), `AcpAgentSession` (maps `initialize`/`session/new`/`session/load`/`session/prompt`/`session/cancel`/`session/update`/`session/close` → `IOmpSession`, permission deny-by-default policy, fs requests return `-32601`), `OmpAcpClient` (factory + version pin 18.12.x).
- `tests/OmpOS.Harness.Acp.Tests`: 30 tests green incl. fixture replay (`acp-in.ndjson`/`acp-out.ndjson` byte-identical), session behaviour, policy deny/never-grant-more, stdio transport errors, handshake timeout, disconnect, clean close, **live smoke against real `omp acp`** (initializes, creates session, closes — verifies `agentInfo.name == "omp"`, capabilities present, session id non-empty). No prompt sent.

## Deviations accepted

- Permission rejection answers with a spec-valid `selected` reject option (or `cancelled`), not `{"outcome":"deny"}` (ACA spec doesn't define that shape).
- `AcpFrames` nests DTOs under one static class (`CA1034` suppressed) for single-file protocol surface.
- `configOptions` opaque as `JsonElement?` (extension data didn't bind under source-gen).
- `Steer` throws `NotSupportedException` (ACP v1 has no steer op).
- Dropped `fs/*` DTOs (client advertises no fs capability; requests return `-32601` tested).
- Handshake timeout enforced in session, not transport (transport is protocol-agnostic).

## Verification

`dotnet test tests/OmpOS.Harness.Acp.Tests` → 30/30, live smoke 0 skipped. Locked restore clean. Architecture tests pass (new projects in L2, no layer violations).