# Phase 2 · Plan 04 Summary — Real omp RPC JSONL client (HARN-05)

**Completed:** 2026-10-20 · **Agent:** sonic `P2Rpc` · **Verified:** orchestrator

## What landed

- `src/OmpOS.Harness.Rpc`: `OmpRpcFrames` (commands, responses, chunks), `OmpRpcJsonContext` (ignore-unknown envelope → typed dispatch), `RpcChunkAssembler` (1 MiB/64 MiB caps, out-of-order index buffering, duplicate detection), `OmpRpcProcess` (spawns `[omp, --mode, rpc, --no-ui, --cwd]`, stdin-close graceful exit, concurrent stderr drain), `OmpRpcSession` (strict id correlation, `prompt` admission vs `prompt_result` completion, `agentInvoked:false` short-circuit, `get_state` version probe), `OmpRpcClient` + `OmpVersionProbe` (18.12.x pin).
- `tests/OmpOS.Harness.Rpc.Tests`: fixture replay (`rpc-out.ndjson`), **8-chunk shuffled reassembly** (synthetic >1 MiB `get_state` response split into 8 out-of-order `rpc_chunk` frames → reassembles correctly), live smoke against real `omp --mode rpc --no-ui` (ready → negotiate v2 → get_state → close stdin → exit 0). Prompt-stream tests added for admission/completion mapping.

## Deviations accepted

- Slnx/slnf registration by orchestrator (file ownership).
- `OmpRpcFrames` is a static constants helper (one file per MA0048).
- No `UnknownFrame` type — unknown discriminators ignored directly.
- Id converter normalizes JSON string/number.
- `ResumeAsync` takes file path (omp's `switch_session` uses path, not id).
- Partial event mapping: only text/thinking deltas and tool start/update/end → `HarnessEvent`.
- Chunk validation enforces structural caps but not canonical split (test needs non-canonical).
- Version pin 18.12.x enforced.

## Verification

`dotnet test tests/OmpOS.Harness.Rpc.Tests` green incl. live smoke 0 skipped. Locked restore clean.