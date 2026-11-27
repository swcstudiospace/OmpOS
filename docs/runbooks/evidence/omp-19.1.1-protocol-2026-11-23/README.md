# omp 19.1.1 protocol probe evidence, 2026-11-23

Phase 79 (plan 79-01) raw evidence behind `docs/architecture/omp-19-reverification.md`. All runs were made
against the installed binary on this host on 2026-11-23, from a throwaway Python probe client (stdin/stdout
pipes, bounded reads, child killed only if it outlived a 10 s EOF grace — never needed). Raw unredacted probe
output stayed in `/tmp` scratch and was not committed (contract rule R5); the committed artifacts are the two
redacted captures named below.

## How "no model turn" was ensured

1. The probe only ever wrote these frames to omp stdin: RPC `negotiate_protocol`, `get_state`, and an unknown
   command `nope`; ACP `initialize`. It never sent RPC `prompt`, ACP `authenticate`/`session/new`/
   `session/prompt`, or any positional message argument.
2. Each run used a fresh `HOME` under `/tmp/omp79final/<transport>/home` (plus XDG dirs inside it), a throwaway
   cwd under `/tmp`, and — for RPC — `--session-dir` under `/tmp` with `--no-session`, so no session file was
   written anywhere.
3. The child environment was the parent environment minus every variable whose name contains `API_KEY`,
   `TOKEN`, `SECRET`, `PASSWORD`, `ANTHROPIC`, `OPENAI` or `_KEY`, and the fresh `HOME` held no stored
   credentials. No provider credential was reachable, so no provider call was possible.
4. `--model opus` (RPC run only) selects a model from the local catalog; selecting is not invoking. It was
   required because a fresh profile has no default model (see "Negative probe").
5. Every run ended by closing stdin (EOF); each process exited on its own within the grace period.

## Version

- argv: `~/.local/bin/omp --version` (home prefix redacted, rule R1)
- stdout: `omp/19.1.1`, exit 0, stderr empty

## Help

- argv: `~/.local/bin/omp --help`, exit 0. Full stdout 19564 bytes,
  sha256 `b4f9b413d56901993fe5f54d9133eaa8f2f23b93050f45634d6647d229aa2dc8` (scratch copy, not committed).
  Protocol-relevant lines, verbatim:
  - `--mode=<value>  Output mode: text (default), json, rpc, or rpc-ui`
  - `--cwd=<value>  Directory to start in (overrides the launch cwd)`
  - `--no-ui  With --mode rpc or rpc-ui: run extensions headless (rpc-ui tool UI remains enabled)`
  - `--session-dir=<value>  Directory for session storage and lookup`
  - `--no-session  Don't save session (ephemeral)`
  - `--model=<value>  Model to use (fuzzy match: "opus", "gpt-5.2"; exact with a provider: "openai/gpt-5.2")`
  - `--profile=<value>  Use an isolated profile for auth, sessions, settings, and caches`
- argv: `~/.local/bin/omp acp --help`, exit 0, stdout `Run omp as an ACP (Agent Client Protocol) server over stdio`.
  Starting `omp acp` without `--help` does not print usage; it waits for `initialize` (see ACP run).

## RPC run (frozen)

- argv (manifest, R1-redacted): `~/.local/bin/omp --mode rpc --no-ui --cwd /tmp/omp79final/rpc/cwd
  --session-dir /tmp/omp79final/rpc/sessions --no-session --model opus`
- Started 2026-11-23T20:22:49Z; duration 1767 ms; exit 0 after stdin EOF; stderr empty; not killed.
- Frames exchanged, in order (`in` = omp stdout, `out` = probe stdin write):

  | seq | dir | t_offset_ms | type | bytes (frozen) | sha256 (line, no LF) |
  |---|---|---|---|---|---|
  | 1 | in | 1648 | `ready` | 168 | see manifest |
  | 2 | out | 1648 | `negotiate_protocol` (`protocolVersion: 2`) | 58 | see manifest |
  | 3 | in | 1658 | `available_commands_update` (53 commands) | 16804 | see manifest |
  | 4 | in | 1659 | `response` to `negotiate_protocol`, `success: true`, `data.protocolVersion: 2` | 103 | see manifest |
  | 5 | out | 1659 | `get_state` | 29 | see manifest |
  | 6 | in | 1685 | `response` to `get_state`, `success: true` (systemPrompt/dumpTools redacted, R4) | 2431 | see manifest |
  | 7 | out | 1685 | `nope` | 24 | see manifest |
  | 8 | in | 1703 | `response` to `nope`, `success: false`, `Unknown command: nope` | 93 | see manifest |

  Exact per-line `line_sha256` values are in the sidecar manifest; this table does not duplicate them to
  avoid a second copy drifting from the frozen bytes.
- Frozen: `tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-observed-19.1.1-20261123T202249Z.ndjson`
  - 19718 bytes, 8 lines, sha256 `29da29f0c7334c3ecb9cda7f4bf9659bf11fe419c98745b9ab4ce42476dd68bd`
  - manifest `rpc-observed-19.1.1-20261123T202249Z.manifest.json`: `live_model_turn: false`,
    `evidence_label: observed-19.1.1`, `ledger_refs: [OQ-520, OQ-521, OQ-522, OQ-523, OQ-524]`,
    `redaction_rules: [R1, R4]` (R1: binary path in argv; R4: `/data/systemPrompt`, `/data/dumpTools` on seq 6)
- Key observed frame, verbatim (seq 1):
  `{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864,"supportsBashExcludeFromContext":true}`

## ACP run (frozen)

- argv (manifest, R1-redacted): `~/.local/bin/omp acp`
- Started 2026-11-23T20:22:51Z; duration 1354 ms; exit 0 after stdin EOF; stderr empty; not killed.
- Sent once: `{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{"fs":{"readTextFile":false,"writeTextFile":false}}}}`
- Received at t_offset 1322 ms, verbatim:
  `{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":1,"agentInfo":{"name":"omp","title":"omp","version":"19.1.1"},"authMethods":[{"id":"agent","name":"Use existing local credentials","description":"Authenticate via the provider keys/OAuth state already configured under ~/.omp."}],"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":true,"sse":true},"promptCapabilities":{"embeddedContext":true,"image":true},"sessionCapabilities":{"list":{},"fork":{},"resume":{},"close":{}}},"_meta":{"steering":{"supported":true}}}}`
- Frozen: `tests/Autonogrammer.Harness.Acp.Tests/TestData/frames/acp-observed-19.1.1-20261123T202251Z.ndjson`
  - 684 bytes, 2 lines, sha256 `5864a2c1a529b77b88904c24ebf38177ddbfb74facf05c729dac31d7dac9400e`
  - manifest `acp-observed-19.1.1-20261123T202251Z.manifest.json`: `live_model_turn: false`,
    `evidence_label: observed-19.1.1`, `ledger_refs: [ACP-INITIALIZE]`, `redaction_rules: [R1]`
    (binary path in argv only; no frame byte changed)

## Negative probe (not frozen)

- argv: `~/.local/bin/omp --mode rpc --no-ui --cwd /tmp/omp79probe/cwd`, fresh `HOME`, credentials scrubbed,
  **no** `--model`.
- Result: exit 1, zero stdout frames, stderr:
  `No default model selected. Use /login, set an API key environment variable, or select a local model with /model or --model.`
  followed by key-creation hints pointing at the fresh profile's `~/.omp/agent/models.yml`.
- `--no-ui` was **accepted** in both RPC runs — the failure above is the missing default model, not a flag
  rejection. Recorded in the ledger under "Startup without a model".

## Drift summary

The full drift table (fact | 18.12.x recorded | 19.1.1 observed | same/changed/unobservable) is the ledger,
`docs/architecture/omp-19-reverification.md`. Headline: two additive changes (`ready.supportsBashExcludeFromContext`,
ACP `_meta.steering.supported`), the ACP version string, three additive `get_state` keys; handshake shapes,
caps, auth method, unknown-command envelope, flags and EOF exit all same; chunk payload cap, prompt-path
commands and any RPC approval surface unobservable-without-model-turn.

## Redaction notes

- R1: `/root/` → `~/` in manifest `argv` and `omp_binary_path` only. `/tmp/omp79final/...` paths contain no
  username and are evidence of the throwaway setup, so they stay.
- R4: seq 6 of the RPC capture, `/data/systemPrompt` and `/data/dumpTools`, replaced with `<redacted>` before
  hashing; pointers recorded in the manifest frame index. All other `get_state` data is verbatim.
- Secret scan (V5 note): the frozen `get_state` model object carries the keys `tokenizer`, `maxTokens` and
  `contextUsage.tokens` — model metadata, not secrets. No key matching a secret-name pattern carries a
  credential value in either capture; nothing was hashed-then-kept.
